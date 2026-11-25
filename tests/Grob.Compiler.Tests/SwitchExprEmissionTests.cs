using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 4 Increment E — the switch <b>expression</b>.
/// </summary>
/// <remarks>
/// <para><b>Emission (D-454).</b> The subject stays on the operand stack and never takes
/// a local slot: a slot counted from the declared locals is wrong when the switch is an
/// operand with values already below it (R-77). Each tested arm copies the subject with
/// <see cref="OpCode.Dup"/>, applies the pattern test (<see cref="OpCode.Equal"/> for a
/// value pattern, a typed comparison for a relational pattern), then
/// <see cref="OpCode.JumpIfFalse"/> to the next arm. A matched arm discards the subject
/// with <see cref="OpCode.Pop"/>, evaluates its result and <see cref="OpCode.Jump"/>s to
/// the end. The final arm is the untested fall-through tail, which pops the subject and
/// evaluates its result; exhaustiveness (proven by the type checker) guarantees it
/// matches. Every path leaves exactly the result on the stack.</para>
/// <para>Unlike <c>select</c>, no trailing <see cref="OpCode.PopN"/> is emitted: the
/// switch is an expression and its single result value remains on the stack.</para>
/// </remarks>
public sealed class SwitchExprEmissionTests {
    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return chunk;
    }

    private static List<OpCode> ReadOpcodes(Chunk chunk) {
        var result = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            result.Add(op);
            switch (op) {
                case OpCode.Constant:
                case OpCode.DefineGlobal:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.GetLocal:
                case OpCode.SetLocal:
                case OpCode.PopN:
                case OpCode.BuildString:
                case OpCode.Call:
                case OpCode.NewArray:
                case OpCode.NewAnonStruct:
                case OpCode.GetProperty:
                case OpCode.SetProperty:
                    offset += 1;
                    break;
                case OpCode.ConstantLong:
                case OpCode.Jump:
                case OpCode.JumpIfFalse:
                case OpCode.JumpIfTrue:
                case OpCode.Loop:
                    offset += 2;
                    break;
            }
            if (op == OpCode.Return) break;
        }
        return result;
    }

    /// <summary>
    /// A value-pattern switch with a <c>_</c> tail compiles to: the subject on the stack,
    /// one Dup / pattern / Equal / JumpIfFalse test, the matched arm popping the subject,
    /// pushing its result and Jump-ing to the end, and the untested tail arm popping the
    /// subject and pushing its result. No trailing PopN — the result stays on the stack.
    /// </summary>
    [Fact]
    public void ValuePatternSwitch_EmitsDupTestThenPopLadder() {
        Chunk chunk = CompileSource("y := 1 switch { 1 => 2, _ => 3 }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [
                OpCode.Constant,    // subject 1, left on the stack
                OpCode.Dup,         // arm 1 test: copy the subject
                OpCode.Constant,    // pattern 1
                OpCode.Equal,
                OpCode.JumpIfFalse, // not equal -> tail arm, subject still on the stack
                OpCode.Pop,         // matched: discard the subject
                OpCode.Constant,    // result 2
                OpCode.Jump,        // -> end
                OpCode.Pop,         // tail arm: discard the subject
                OpCode.Constant,    // tail arm result 3 (untested)
                OpCode.DefineGlobal,// bind y
                OpCode.Return,
            ],
            ops);
    }

    /// <summary>
    /// The R-77 shape: a switch as the right operand of <c>+</c>. The left operand stays
    /// below the subject untouched, no local slot is read or written, and
    /// <see cref="OpCode.AddInt"/> meets the left operand and the arm's result.
    /// </summary>
    [Fact]
    public void Switch_AsRightOperand_KeepsTheLeftOperandBelowTheSubject() {
        Chunk chunk = CompileSource("y := 1 + (5 switch { 0 => 1, _ => 2 })");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [
                OpCode.Constant,    // left operand 1
                OpCode.Constant,    // subject 5
                OpCode.Dup,
                OpCode.Constant,    // pattern 0
                OpCode.Equal,
                OpCode.JumpIfFalse,
                OpCode.Pop,
                OpCode.Constant,    // result 1
                OpCode.Jump,
                OpCode.Pop,
                OpCode.Constant,    // tail result 2
                OpCode.AddInt,      // left operand + switch value
                OpCode.DefineGlobal,
                OpCode.Return,
            ],
            ops);
    }

    /// <summary>
    /// With operands already on the stack — a native's argument, a method's receiver, an
    /// anonymous struct's field, an interpolated part, an assignment's value and a
    /// compound assignment's value — the switch reads and writes no local slot of its
    /// own: one Dup per tested arm and one Pop per arm.
    /// </summary>
    [Theory]
    [InlineData("print(math.pow(5 switch { 0 => 1.5, _ => 2.5 }, 1))")]
    [InlineData("print((10 switch { 0 => \"\", _ => \"a\" }).upper())")]
    [InlineData("s := #{ m0: 5 switch { 0 => 1.5, _ => 2.5 } }")]
    [InlineData("print(\"v=${4 switch { 0 => \"zero\", _ => \"?\" }}\")")]
    [InlineData("xs := [5, 6]\nxs[0] = (\"Hello\" switch { \"k0\" => 8, _ => 1 })")]
    public void Switch_WithOperandsBelow_UsesNoLocalSlot(string source) {
        Chunk chunk = CompileSource(source);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.GetLocal, ops);
        Assert.DoesNotContain(OpCode.SetLocal, ops);
        Assert.Equal(1, ops.Count(o => o == OpCode.Dup));
        Assert.Equal(2, ops.Count(o => o == OpCode.Pop));
    }

    /// <summary>
    /// A compound index assignment keeps its receiver and index in two temporary locals
    /// and pushes four operands above them before its value runs. A switch as that value
    /// writes no local slot, so none of those operands is overwritten.
    /// </summary>
    [Fact]
    public void Switch_AsCompoundIndexAssignmentValue_WritesNoLocalSlot() {
        Chunk chunk = CompileSource("xs := [10, 20]\nxs[1] += (3 switch { 3 => 5, _ => 0 })");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.SetLocal, ops);
        Assert.Equal(1, ops.Count(o => o == OpCode.Dup));
    }

    /// <summary>
    /// No trailing <see cref="OpCode.PopN"/> is emitted — the switch leaves its single
    /// result on the stack (it is an expression, not the <c>select</c> statement).
    /// </summary>
    [Fact]
    public void Switch_LeavesValue_EmitsNoSubjectPop() {
        Chunk chunk = CompileSource("y := 1 switch { 1 => 2, _ => 3 }");
        Assert.DoesNotContain(OpCode.PopN, ReadOpcodes(chunk));
    }

    /// <summary>
    /// Each tested arm copies the subject once and every arm pops it once, so every path
    /// converges with one value on the stack and no local slot.
    /// </summary>
    [Fact]
    public void Switch_EveryArm_PopsTheSubjectOnce() {
        Chunk chunk = CompileSource("y := 1 switch { 1 => 2, 2 => 3, _ => 4 }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(2, ops.Count(o => o == OpCode.Dup));   // two tested arms
        Assert.Equal(3, ops.Count(o => o == OpCode.Pop));   // one per arm (two tested, one tail)
        Assert.Equal(2, ops.Count(o => o == OpCode.Equal)); // two tested arms
        Assert.DoesNotContain(OpCode.SetLocal, ops);
    }

    /// <summary>
    /// A relational pattern on <c>int</c> compiles to the typed comparison opcode
    /// (<see cref="OpCode.GreaterEqualInt"/>) for the <c>&gt;=</c> arm.
    /// </summary>
    [Fact]
    public void RelationalIntPattern_EmitsTypedComparison() {
        Chunk chunk = CompileSource("y := 5 switch { >= 10 => 1, _ => 0 }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.GreaterEqualInt, ops);
    }

    /// <summary>
    /// String <c>&gt;=</c> has no dedicated opcode — it lowers to the strict
    /// <see cref="OpCode.LessString"/> followed by <see cref="OpCode.Not"/>
    /// (<c>a &gt;= b ≡ !(a &lt; b)</c>), mirroring the binary-expression lowering.
    /// </summary>
    [Fact]
    public void RelationalStringGreaterEqualPattern_LowersToLessStringNot() {
        Chunk chunk = CompileSource("y := \"m\" switch { >= \"n\" => 1, _ => 0 }");
        List<OpCode> ops = ReadOpcodes(chunk);
        int less = ops.IndexOf(OpCode.LessString);
        Assert.True(less >= 0, "expected LessString in the lowered '>=' test");
        Assert.Equal(OpCode.Not, ops[less + 1]);
    }
}
