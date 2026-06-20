using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 4 Increment E — the switch <b>expression</b>.
/// </summary>
/// <remarks>
/// <para><b>Emission.</b> The subject is evaluated once into a synthetic local. Each
/// non-final arm re-loads it with <see cref="OpCode.GetLocal"/>, applies the pattern
/// test (<see cref="OpCode.Equal"/> for a value pattern, a typed comparison for a
/// relational pattern), then <see cref="OpCode.JumpIfFalse"/> to the next arm. A
/// matched arm evaluates its result, stores it back into the subject slot with
/// <see cref="OpCode.SetLocal"/> — leaving exactly one value on the stack — then
/// <see cref="OpCode.Jump"/>s to the end. The final arm is the untested fall-through
/// tail; exhaustiveness (proven by the type checker) guarantees it matches.</para>
/// <para>Unlike <c>select</c>, no trailing <see cref="OpCode.PopN"/> is emitted: the
/// switch is an expression and its single result value remains on the stack.</para>
/// </remarks>
public sealed class SwitchExprEmissionTests {
    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
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
    /// A value-pattern switch with a <c>_</c> tail compiles to: subject into the slot,
    /// one GetLocal / pattern / Equal / JumpIfFalse test, the matched arm storing its
    /// result with SetLocal then Jump-ing to the end, and the untested tail arm storing
    /// its result. No trailing PopN — the result stays on the stack.
    /// </summary>
    [Fact]
    public void ValuePatternSwitch_EmitsTestThenSetLocalLadder() {
        Chunk chunk = CompileSource("y := 1 switch { 1 => 2, _ => 3 }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [
                OpCode.Constant,    // subject 1 -> $subject slot
                OpCode.GetLocal,    // arm 1 test: load subject
                OpCode.Constant,    // pattern 1
                OpCode.Equal,
                OpCode.JumpIfFalse, // not equal -> tail arm
                OpCode.Constant,    // result 2
                OpCode.SetLocal,    // store result into subject slot
                OpCode.Jump,        // matched -> end
                OpCode.Constant,    // tail arm result 3 (untested)
                OpCode.SetLocal,    // store result into subject slot
                OpCode.DefineGlobal,// bind y
                OpCode.Return,
            ],
            ops);
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
    /// Exactly one SetLocal per arm stores the result back into the subject slot, so
    /// every path converges with one value on the stack.
    /// </summary>
    [Fact]
    public void Switch_EveryArm_StoresResultIntoSubjectSlot() {
        Chunk chunk = CompileSource("y := 1 switch { 1 => 2, 2 => 3, _ => 4 }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(3, ops.Count(o => o == OpCode.SetLocal)); // one per arm (two tested, one tail)
        Assert.Equal(2, ops.Count(o => o == OpCode.Equal));    // two tested arms
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
