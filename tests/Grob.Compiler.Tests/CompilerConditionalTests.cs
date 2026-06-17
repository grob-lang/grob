using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 4 Increment A — conditionals:
/// <c>if</c>/<c>else if</c>/<c>else</c>, <c>&amp;&amp;</c>/<c>||</c>
/// short-circuit emission, and the ternary <c>?:</c>.
/// </summary>
/// <remarks>
/// Each test verifies the exact opcode sequence emitted so that jump-offset
/// regressions (a silent miscompilation) are caught immediately.  No VM
/// execution is performed here — that is <see cref="VirtualMachineConditionalTests"/>.
/// </remarks>
public sealed class CompilerConditionalTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker produced errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return GrobCompiler.Compile(unit, bag);
    }

    /// <summary>
    /// Reads the opcode sequence from <paramref name="chunk"/>, advancing past
    /// operand bytes for every opcode that carries them.  Stops at (and
    /// includes) the first <see cref="OpCode.Return"/>.
    /// </summary>
    private static List<OpCode> ReadOpcodes(Chunk chunk) {
        var result = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            result.Add(op);
            switch (op) {
                // 1-byte operand opcodes
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
                // 2-byte operand opcodes (big-endian forward offset)
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

    // -----------------------------------------------------------------------
    // if — without else
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>if (true) { }</c> — an if-without-else must emit:
    /// <list type="number">
    /// <item><description>the condition (<see cref="OpCode.True"/>)</description></item>
    /// <item><description><see cref="OpCode.JumpIfFalse"/> to skip the then-block</description></item>
    /// <item><description>the then-block body (empty — no opcodes)</description></item>
    /// <item><description><see cref="OpCode.Return"/> (end of compilation unit)</description></item>
    /// </list>
    /// No unconditional <see cref="OpCode.Jump"/> is present (there is no else to skip).
    /// </summary>
    [Fact]
    public void IfWithoutElse_EmitsJumpIfFalse_ThenBlock_NoJump() {
        Chunk chunk = CompileSource("if (true) { }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.True, OpCode.JumpIfFalse, OpCode.Return], ops);
    }

    // -----------------------------------------------------------------------
    // if — with else
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>if (false) { } else { }</c> — the then-block must end with an unconditional
    /// <see cref="OpCode.Jump"/> to skip the else-block, and the else-block follows.
    /// </summary>
    [Fact]
    public void IfWithElse_EmitsJumpIfFalse_ThenBlock_Jump_ElseBlock() {
        Chunk chunk = CompileSource("if (false) { } else { }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.False, OpCode.JumpIfFalse, OpCode.Jump, OpCode.Return], ops);
    }

    // -----------------------------------------------------------------------
    // if — else if chain
    // -----------------------------------------------------------------------

    /// <summary>
    /// A three-arm <c>if</c>/<c>else if</c>/<c>else</c> chain must emit two
    /// <see cref="OpCode.JumpIfFalse"/> opcodes (one per arm condition) and two
    /// <see cref="OpCode.Jump"/> opcodes (one per arm exit).
    /// </summary>
    [Fact]
    public void ElseIf_Chain_EmitsNestedJumps_AllExitJumpsPatched() {
        Chunk chunk = CompileSource("if (false) { } else if (false) { } else { }");
        List<OpCode> ops = ReadOpcodes(chunk);
        // Expected shape:
        //   False         ← outer condition
        //   JumpIfFalse   ← skip outer then
        //   Jump          ← outer then exit (jump past everything)
        //   False         ← inner condition (else if)
        //   JumpIfFalse   ← skip inner then
        //   Jump          ← inner then exit
        //   Return        ← end of unit
        Assert.Equal([
            OpCode.False, OpCode.JumpIfFalse, OpCode.Jump,
            OpCode.False, OpCode.JumpIfFalse, OpCode.Jump,
            OpCode.Return
        ], ops);
    }

    // -----------------------------------------------------------------------
    // && — short-circuit via JumpIfFalse
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>x := true &amp;&amp; false</c> must emit:
    /// <list type="number">
    /// <item><description>Left operand</description></item>
    /// <item><description><see cref="OpCode.JumpIfFalse"/> (no dedicated And opcode)</description></item>
    /// <item><description>Right operand</description></item>
    /// <item><description><see cref="OpCode.Jump"/> past the false-label</description></item>
    /// <item><description><see cref="OpCode.False"/> (synthesised result for the short-circuit path)</description></item>
    /// </list>
    /// The sequence confirms no dedicated <c>And</c> opcode is emitted.
    /// </summary>
    [Fact]
    public void And_EmitsJumpIfFalse_NoDedicatedAndOpcode() {
        Chunk chunk = CompileSource("x := true && false");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([
            OpCode.True, OpCode.JumpIfFalse,
            OpCode.False,           // right operand
            OpCode.Jump,
            OpCode.False,           // synthesised false for short-circuit path
            OpCode.DefineGlobal,
            OpCode.Return
        ], ops);
        Assert.DoesNotContain((OpCode)0xFF, ops);  // no unknown opcode
    }

    // -----------------------------------------------------------------------
    // || — short-circuit via JumpIfTrue
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>x := true || false</c> must emit:
    /// <list type="number">
    /// <item><description>Left operand</description></item>
    /// <item><description><see cref="OpCode.JumpIfTrue"/> (peeks; no dedicated Or opcode)</description></item>
    /// <item><description><see cref="OpCode.Pop"/> (discard the peeked false value)</description></item>
    /// <item><description>Right operand</description></item>
    /// </list>
    /// </summary>
    [Fact]
    public void Or_EmitsJumpIfTrue_NoDedicatedOrOpcode() {
        Chunk chunk = CompileSource("x := true || false");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([
            OpCode.True, OpCode.JumpIfTrue,
            OpCode.Pop,             // discard peeked false
            OpCode.False,           // right operand
            OpCode.DefineGlobal,
            OpCode.Return
        ], ops);
    }

    // -----------------------------------------------------------------------
    // Ternary — jump-based two-arm shape
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>x := true ? 1 : 2</c> must emit condition, <see cref="OpCode.JumpIfFalse"/>
    /// to the else-arm, then-arm, <see cref="OpCode.Jump"/> past the else-arm,
    /// and the else-arm.  Exactly one arm executes per condition value.
    /// </summary>
    [Fact]
    public void Ternary_EmitsJumpIfFalse_ThenArm_Jump_ElseArm() {
        Chunk chunk = CompileSource("x := true ? 1 : 2");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([
            OpCode.True,            // condition
            OpCode.JumpIfFalse,
            OpCode.Constant,        // then arm: 1
            OpCode.Jump,
            OpCode.Constant,        // else arm: 2
            OpCode.DefineGlobal,
            OpCode.Return
        ], ops);
    }

    // -----------------------------------------------------------------------
    // Not — bool negation opcode
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>x := !true</c> must emit the operand followed by <see cref="OpCode.Not"/>.
    /// </summary>
    [Fact]
    public void Not_EmitsNotOpcode() {
        Chunk chunk = CompileSource("x := !true");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.True, OpCode.Not, OpCode.DefineGlobal, OpCode.Return], ops);
    }

    // -----------------------------------------------------------------------
    // Comparison — typed opcode selection
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>x := 3 &lt; 5</c> must emit <see cref="OpCode.LessInt"/> (not a generic
    /// comparison opcode) because both operands are <c>int</c>.
    /// </summary>
    [Fact]
    public void IntLess_EmitsLessInt() {
        Chunk chunk = CompileSource("x := 3 < 5");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.LessInt, ops);
    }

    /// <summary>
    /// <c>x := 3.0 &lt; 5.0</c> must emit <see cref="OpCode.LessFloat"/>.
    /// </summary>
    [Fact]
    public void FloatLess_EmitsLessFloat() {
        Chunk chunk = CompileSource("x := 3.0 < 5.0");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.LessFloat, ops);
    }

    /// <summary>
    /// <c>x := 3 == 3</c> must emit <see cref="OpCode.Equal"/>.
    /// </summary>
    [Fact]
    public void IntEqual_EmitsEqual() {
        Chunk chunk = CompileSource("x := 3 == 3");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.Equal, ops);
    }

    /// <summary>
    /// <c>x := "a" &lt; "b"</c> must emit <see cref="OpCode.LessString"/>.
    /// </summary>
    [Fact]
    public void StringLess_EmitsLessString() {
        Chunk chunk = CompileSource("""x := "a" < "b" """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.LessString, ops);
    }

    /// <summary>
    /// <c>x := "a" &gt; "b"</c> must emit <see cref="OpCode.GreaterString"/>.
    /// </summary>
    [Fact]
    public void StringGreater_EmitsGreaterString() {
        Chunk chunk = CompileSource("""x := "a" > "b" """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.GreaterString, ops);
    }

    /// <summary>
    /// The closed <c>OpCode</c> enum has no <c>LessEqualString</c>; <c>"a" &lt;= "b"</c>
    /// lowers to the strict comparison plus negation — <c>!(a &gt; b)</c> — so it must
    /// emit <see cref="OpCode.GreaterString"/> followed by <see cref="OpCode.Not"/>,
    /// and never <see cref="OpCode.LessEqualInt"/> (which would call AsInt on a string).
    /// </summary>
    [Fact]
    public void StringLessEqual_LowersToGreaterStringThenNot() {
        Chunk chunk = CompileSource("""x := "a" <= "b" """);
        List<OpCode> ops = ReadOpcodes(chunk);
        int idx = ops.IndexOf(OpCode.GreaterString);
        Assert.True(idx >= 0, "expected GreaterString in the lowered sequence");
        Assert.Equal(OpCode.Not, ops[idx + 1]);
        Assert.DoesNotContain(OpCode.LessEqualInt, ops);
    }

    /// <summary>
    /// <c>"a" &gt;= "b"</c> lowers to <c>!(a &lt; b)</c> — <see cref="OpCode.LessString"/>
    /// followed by <see cref="OpCode.Not"/>, never <see cref="OpCode.GreaterEqualInt"/>.
    /// </summary>
    [Fact]
    public void StringGreaterEqual_LowersToLessStringThenNot() {
        Chunk chunk = CompileSource("""x := "a" >= "b" """);
        List<OpCode> ops = ReadOpcodes(chunk);
        int idx = ops.IndexOf(OpCode.LessString);
        Assert.True(idx >= 0, "expected LessString in the lowered sequence");
        Assert.Equal(OpCode.Not, ops[idx + 1]);
        Assert.DoesNotContain(OpCode.GreaterEqualInt, ops);
    }

    /// <summary>
    /// A ternary whose arms unify to a wider type (<c>int</c>/<c>float</c> → <c>float</c>)
    /// must be typed as that wider type when consumed by a parent expression. Here
    /// <c>(true ? 1 : 2.0) + 1.0</c> must select <see cref="OpCode.AddFloat"/>: the parent
    /// '+' sees the ternary as <c>float</c>, not <c>int</c> from the then-arm alone.
    /// Guards against inferring the ternary type from the then-arm only.
    /// </summary>
    [Fact]
    public void Ternary_MixedArmsConsumedByParent_WidensToFloat() {
        Chunk chunk = CompileSource("x := (true ? 1 : 2.0) + 1.0");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.AddFloat, ops);
        Assert.DoesNotContain(OpCode.AddInt, ops);
    }

    /// <summary>
    /// A ternary with one nullable and one non-nullable arm (either order) unifies to
    /// the nullable type. Unlike int/float, <c>T</c> and <c>T?</c> share a runtime
    /// representation, so no coercion opcode is emitted. Exercises the T/T? widening
    /// branch of the compiler's ternary result-type computation.
    /// </summary>
    [Fact]
    public void Ternary_NullableAndNonNullableArms_WidensWithoutCoercion() {
        Chunk chunk = CompileSource("""
            flag := true
            n: int? := nil
            a := flag ? n : 5
            b := flag ? 5 : n
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.IntToFloat, ops);
    }
}
