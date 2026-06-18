using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape and diagnostic tests for Sprint 4 Increment B — <c>while</c>
/// loops and <c>break</c> / <c>continue</c> loop control.
/// </summary>
/// <remarks>
/// Tests verify:
/// <list type="bullet">
/// <item><description>The type checker's bool-condition validation for <c>while</c>.</description></item>
/// <item><description>E2211 / E2212 diagnostics when <c>break</c> / <c>continue</c>
///   appear outside a loop.</description></item>
/// <item><description>The exact opcode sequence and jump-offset mechanics emitted for
///   <c>while</c>, <c>break</c> and <c>continue</c>, so that silent
///   miscompilations are caught immediately.</description></item>
/// </list>
/// VM execution is not performed here — that is <see cref="VirtualMachineLoopTests"/>
/// and <c>Sprint4IncrementBTests</c>.
/// </remarks>
public sealed class CompilerLoopTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs the full compile pipeline and asserts no diagnostic errors.
    /// Returns the compiled <see cref="Chunk"/>.
    /// </summary>
    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return GrobCompiler.Compile(unit, bag);
    }

    /// <summary>
    /// Runs Lexer → Parser → TypeChecker without asserting success and without
    /// compiling to bytecode.  Used to test diagnostic cases.
    /// </summary>
    private static DiagnosticBag TypeCheckSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
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
                case OpCode.IncrementInt:
                case OpCode.DecrementInt:
                case OpCode.BuildString:
                    offset += 1;
                    break;
                // 2-byte operand opcodes (big-endian offset)
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
    // Type-checker — while condition must be bool
    // -----------------------------------------------------------------------

    /// <summary>
    /// A non-<c>bool</c> <c>while</c> condition must produce E0001 (type mismatch).
    /// </summary>
    [Fact]
    public void While_NonBoolCondition_ProducesE0001() {
        DiagnosticBag bag = TypeCheckSource("while (42) { }");
        Assert.True(bag.HasErrors);
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E0001");
        Assert.Equal(1, diag.Range.Start.Line);
    }

    // -----------------------------------------------------------------------
    // Type-checker — break / continue outside a loop
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>break</c> at the top level (outside any loop) must produce E2211
    /// with the correct source location.
    /// </summary>
    [Fact]
    public void Break_OutsideLoop_ProducesE2211() {
        DiagnosticBag bag = TypeCheckSource("break");
        Assert.True(bag.HasErrors);
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E2211");
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    /// <summary>
    /// <c>continue</c> at the top level (outside any loop) must produce E2212
    /// with the correct source location.
    /// </summary>
    [Fact]
    public void Continue_OutsideLoop_ProducesE2212() {
        DiagnosticBag bag = TypeCheckSource("continue");
        Assert.True(bag.HasErrors);
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E2212");
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    /// <summary>
    /// <c>break</c> inside an <c>if</c> that is not inside any loop must still
    /// produce E2211 — the <c>if</c> block does not create a loop context.
    /// </summary>
    [Fact]
    public void Break_InsideIfButOutsideLoop_ProducesE2211() {
        DiagnosticBag bag = TypeCheckSource("if (true) { break }");
        Assert.True(bag.HasErrors);
        Assert.Single(bag.Errors, e => e.Code == "E2211");
    }

    /// <summary>
    /// <c>continue</c> inside an <c>if</c> that is not inside any loop must still
    /// produce E2212.
    /// </summary>
    [Fact]
    public void Continue_InsideIfButOutsideLoop_ProducesE2212() {
        DiagnosticBag bag = TypeCheckSource("if (true) { continue }");
        Assert.True(bag.HasErrors);
        Assert.Single(bag.Errors, e => e.Code == "E2212");
    }

    /// <summary>
    /// <c>break</c> inside a <c>while</c> is legal — no E2211 diagnostic.
    /// </summary>
    [Fact]
    public void Break_InsideWhile_IsLegal() {
        DiagnosticBag bag = TypeCheckSource("while (true) { break }");
        Assert.False(bag.HasErrors);
    }

    /// <summary>
    /// <c>continue</c> inside a <c>while</c> is legal — no E2212 diagnostic.
    /// </summary>
    [Fact]
    public void Continue_InsideWhile_IsLegal() {
        DiagnosticBag bag = TypeCheckSource("while (true) { continue }");
        Assert.False(bag.HasErrors);
    }

    // -----------------------------------------------------------------------
    // Compiler — while opcode sequence
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>while (true) { }</c> — an empty while must emit:
    /// <list type="number">
    /// <item><description><see cref="OpCode.True"/> (condition)</description></item>
    /// <item><description><see cref="OpCode.JumpIfFalse"/> to skip past the loop</description></item>
    /// <item><description><see cref="OpCode.Loop"/> backward to the condition</description></item>
    /// <item><description><see cref="OpCode.Return"/></description></item>
    /// </list>
    /// No body opcodes; no unconditional <see cref="OpCode.Jump"/>.
    /// </summary>
    [Fact]
    public void While_EmptyBody_EmitsConditionJumpIfFalseLoop() {
        Chunk chunk = CompileSource("while (true) { }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.True, OpCode.JumpIfFalse, OpCode.Loop, OpCode.Return], ops);
    }

    /// <summary>
    /// <c>while (true) { break }</c> — a break inside the body must emit an
    /// unconditional <see cref="OpCode.Jump"/> (forward, to after the loop) before
    /// the end-of-body <see cref="OpCode.Loop"/>.
    /// </summary>
    [Fact]
    public void While_WithBreak_EmitsJump_ThenLoop() {
        Chunk chunk = CompileSource("while (true) { break }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.True, OpCode.JumpIfFalse, OpCode.Jump, OpCode.Loop, OpCode.Return], ops);
    }

    /// <summary>
    /// <c>while (true) { continue }</c> — a continue inside the body must emit a
    /// <see cref="OpCode.Loop"/> backward to the condition immediately, followed by
    /// the end-of-body <see cref="OpCode.Loop"/> (the continue one runs first at
    /// runtime; both land at the same loop-top target).
    /// </summary>
    [Fact]
    public void While_WithContinue_EmitsLoop_ThenLoop() {
        Chunk chunk = CompileSource("while (true) { continue }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([OpCode.True, OpCode.JumpIfFalse, OpCode.Loop, OpCode.Loop, OpCode.Return], ops);
    }

    /// <summary>
    /// Two <c>break</c> statements inside a single <c>while</c> must each emit
    /// a forward <see cref="OpCode.Jump"/>; both jumps must patch to the same
    /// exit target (the instruction after the end-of-body <see cref="OpCode.Loop"/>).
    /// </summary>
    [Fact]
    public void While_TwoBreaks_BothPatchToSameExit() {
        // while (true) { break \n break } — two breaks on separate lines, same exit target
        Chunk chunk = CompileSource("""
            while (true) {
                break
                break
            }
            """);
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal(
            [OpCode.True, OpCode.JumpIfFalse, OpCode.Jump, OpCode.Jump, OpCode.Loop, OpCode.Return],
            ops);

        // Verify both Jump offsets land at the same byte offset (the Return).
        // Layout: True[0] JumpIfFalse[1,2,3] Jump[4,5,6] Jump[7,8,9] Loop[10,11,12] Return[13]
        // Both Jump[4] and Jump[7] must patch to Return[13].
        // Jump[4]: ip after operands = 7; target = 13; offset = 13 - 7 = 6
        // Jump[7]: ip after operands = 10; target = 13; offset = 13 - 10 = 3
        int break1Hi = chunk.ReadByte(5);
        int break1Lo = chunk.ReadByte(6);
        int break1Offset = (break1Hi << 8) | break1Lo;

        int break2Hi = chunk.ReadByte(8);
        int break2Lo = chunk.ReadByte(9);
        int break2Offset = (break2Hi << 8) | break2Lo;

        // The first break's forward jump: ip_after_operands(7) + offset1 must == 13
        Assert.Equal(13, 7 + break1Offset);
        // The second break's forward jump: ip_after_operands(10) + offset2 must == 13
        Assert.Equal(13, 10 + break2Offset);
    }

    /// <summary>
    /// The <see cref="OpCode.Loop"/> at the end of a <c>while</c> body must have
    /// an offset that sends execution exactly back to the condition (the first
    /// instruction of the loop), not one before or after it.
    /// </summary>
    [Fact]
    public void While_LoopOffset_LandsAtCondition() {
        // while (true) { } — True[0] JumpIfFalse[1,2,3] Loop[4,5,6] Return[7]
        // Loop at offset 4: ip after operands = 7; offset = 7; target = 7 - 7 = 0 = True ✓
        Chunk chunk = CompileSource("while (true) { }");

        byte loopHi = chunk.ReadByte(5);
        byte loopLo = chunk.ReadByte(6);
        int loopOffset = (loopHi << 8) | loopLo;

        // ip after reading Loop's operands (at 7) minus loopOffset must be 0 (True).
        Assert.Equal(0, 7 - loopOffset);
    }

    /// <summary>
    /// A <c>continue</c> inside a <c>while</c> must emit a <see cref="OpCode.Loop"/>
    /// that jumps back to the <em>condition</em> (the same target as the
    /// end-of-body <see cref="OpCode.Loop"/>).
    /// </summary>
    [Fact]
    public void While_ContinueLoopOffset_LandsAtCondition() {
        // while (true) { continue }
        // True[0] JumpIfFalse[1,2,3] Loop_continue[4,5,6] Loop_body[7,8,9] Return[10]
        // Loop_continue at 4: ip after = 7; offset must send to 0 (True); offset = 7
        // Loop_body at 7: ip after = 10; offset must send to 0 (True); offset = 10
        Chunk chunk = CompileSource("while (true) { continue }");

        byte contHi = chunk.ReadByte(5);
        byte contLo = chunk.ReadByte(6);
        int continueOffset = (contHi << 8) | contLo;

        byte bodyHi = chunk.ReadByte(8);
        byte bodyLo = chunk.ReadByte(9);
        int bodyOffset = (bodyHi << 8) | bodyLo;

        // Both should land at position 0 (the True condition).
        Assert.Equal(0, 7 - continueOffset);
        Assert.Equal(0, 10 - bodyOffset);
    }

    // -----------------------------------------------------------------------
    // Compiler — nested while resolves break/continue to innermost loop
    // -----------------------------------------------------------------------

    /// <summary>
    /// A <c>break</c> inside the inner loop of a nested <c>while</c> must patch
    /// to the inner loop's exit, not the outer loop's exit.
    /// </summary>
    /// <remarks>
    /// Opcode sequence for <c>while (false) { while (false) { break } }</c>:
    /// <list type="bullet">
    /// <item><description>False (outer cond), JumpIfFalse (outer exit)</description></item>
    /// <item><description>False (inner cond), JumpIfFalse (inner exit)</description></item>
    /// <item><description>Jump (inner break), Loop (inner body), Loop (outer body)</description></item>
    /// <item><description>Return</description></item>
    /// </list>
    /// </remarks>
    [Fact]
    public void NestedWhile_InnerBreak_PatchesToInnerExit() {
        Chunk chunk = CompileSource("while (false) { while (false) { break } }");
        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Equal([
            OpCode.False, OpCode.JumpIfFalse,
            OpCode.False, OpCode.JumpIfFalse,
            OpCode.Jump,
            OpCode.Loop,
            OpCode.Loop,
            OpCode.Return,
        ], ops);

        // Layout (byte offsets):
        // [0]  False (outer cond)
        // [1]  JumpIfFalse outer
        // [2,3] outer exit offset
        // [4]  False (inner cond)
        // [5]  JumpIfFalse inner
        // [6,7] inner exit offset
        // [8]  Jump (inner break)
        // [9,10] break offset
        // [11] Loop (inner body)
        // [12,13] inner loop offset
        // [14] Loop (outer body)
        // [15,16] outer loop offset
        // [17] Return

        // Inner break's Jump: ip after operands = 11; target must be AFTER inner Loop = 14
        int breakHi = chunk.ReadByte(9);
        int breakLo = chunk.ReadByte(10);
        int breakOffset = (breakHi << 8) | breakLo;
        Assert.Equal(14, 11 + breakOffset);

        // Inner exit JumpIfFalse: ip after operands = 8; target must also be 14
        int innerExitHi = chunk.ReadByte(6);
        int innerExitLo = chunk.ReadByte(7);
        int innerExitOffset = (innerExitHi << 8) | innerExitLo;
        Assert.Equal(14, 8 + innerExitOffset);
    }
}
