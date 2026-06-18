using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class Compiler {
    // -----------------------------------------------------------------------
    // while / break / continue  (Sprint 4 Increment B)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Compiles a <c>while</c> statement using the loop-context stack and the
    /// backward-jump <see cref="EmitLoop"/> helper:
    /// <list type="number">
    ///   <item><description>Record <c>loopStart</c> (the chunk offset of the first
    ///     condition byte) and push a new <see cref="LoopContext"/>.</description></item>
    ///   <item><description>Emit the condition expression.</description></item>
    ///   <item><description>Emit <see cref="OpCode.JumpIfFalse"/> (forward, backpatched)
    ///     to exit the loop when the condition is false.</description></item>
    ///   <item><description>Emit the body.  <see cref="VisitBlock"/> handles <c>PopN</c>
    ///     for locals declared inside the body on the normal exit path.</description></item>
    ///   <item><description>Emit <see cref="OpCode.Loop"/> (backward to <c>loopStart</c>)
    ///     to re-evaluate the condition.</description></item>
    ///   <item><description>Patch the exit <see cref="OpCode.JumpIfFalse"/> to
    ///     land here (the first instruction past the loop).</description></item>
    ///   <item><description>Pop the loop context and backpatch every recorded
    ///     <c>break</c> jump to the same exit target.</description></item>
    /// </list>
    /// </remarks>
    public override object? VisitWhile(WhileStmt node) {
        int line = node.Range.Start.Line;

        // Record the loop top: the first byte of the condition expression.
        int loopStart = _chunk.Count;

        // Push a loop context so that break/continue inside the body resolve here.
        var ctx = new LoopContext(continueTarget: loopStart, baseSlot: _nextSlot);
        _loopContexts.Push(ctx);

        // Evaluate the condition; JumpIfFalse pops it and exits when false.
        Visit(node.Condition);
        int exitJump = EmitJump(OpCode.JumpIfFalse, line);

        // Body — VisitBlock opens a scope, emits body statements, emits PopN on normal exit.
        Visit(node.Body);

        // End of body: jump back to the condition.
        EmitLoop(loopStart, line);

        // Patch the exit jump to land here (first instruction after the loop).
        PatchJump(exitJump);

        // Pop the loop context and patch all break jumps to the same exit target.
        _loopContexts.Pop();
        foreach (int breakSite in ctx.BreakSites)
            PatchJump(breakSite);

        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Emits a forward <see cref="OpCode.Jump"/> that exits the innermost loop,
    /// after popping any locals declared inside the loop body above this point.
    /// The jump site is recorded on the current <see cref="LoopContext"/> and
    /// backpatched when the loop closes in <see cref="VisitWhile"/> (or the
    /// equivalent for...in lowering in Increment C).
    /// </remarks>
    public override object? VisitBreak(BreakStmt node) {
        if (_loopContexts.Count == 0)
            throw new GrobInternalException(
                "break outside loop reached the compiler. The type checker should have rejected this source.");

        LoopContext ctx = _loopContexts.Peek();
        int line = node.Range.Start.Line;

        // Pop locals declared inside the loop above this break — they are not
        // cleaned up by VisitBlock because the break jumps past the block exit.
        int localsToPop = _nextSlot - ctx.BaseSlot;
        if (localsToPop > 0) {
            _chunk.WriteOpCode(OpCode.PopN, line);
            _chunk.WriteByte(ToByteOperand(localsToPop, "break locals pop"), line);
        }

        // Forward jump; patch site is recorded and resolved when the loop closes.
        int site = EmitJump(OpCode.Jump, line);
        ctx.RecordBreak(site);
        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Jumps backward to the loop's continue target via <see cref="OpCode.Loop"/>,
    /// after popping any locals declared inside the loop body above this point.
    /// For <c>while</c> the target is the loop top (the condition).  The target is
    /// taken from <see cref="LoopContext.ContinueTarget"/> rather than hard-coded,
    /// so the <c>for...in</c> lowering in Increment C can point <c>continue</c> at
    /// the increment step instead.
    /// </remarks>
    public override object? VisitContinue(ContinueStmt node) {
        if (_loopContexts.Count == 0)
            throw new GrobInternalException(
                "continue outside loop reached the compiler. The type checker should have rejected this source.");

        LoopContext ctx = _loopContexts.Peek();
        int line = node.Range.Start.Line;

        // Pop locals declared inside the loop above this continue — they are not
        // cleaned up by VisitBlock because the continue jumps past the block exit.
        int localsToPop = _nextSlot - ctx.BaseSlot;
        if (localsToPop > 0) {
            _chunk.WriteOpCode(OpCode.PopN, line);
            _chunk.WriteByte(ToByteOperand(localsToPop, "continue locals pop"), line);
        }

        EmitLoop(ctx.ContinueTarget, line);
        return null;
    }

    // -----------------------------------------------------------------------
    // if / else if / else
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Compiles an <c>if</c> statement using forward-jump backpatching (EmitJump / PatchJump):
    /// <list type="number">
    ///   <item><description>Emit the condition.</description></item>
    ///   <item><description>Emit <see cref="OpCode.JumpIfFalse"/> with a placeholder offset
    ///     to skip the then-block.  JumpIfFalse pops the condition.</description></item>
    ///   <item><description>Emit the then-block body.</description></item>
    ///   <item><description>If there is an else clause, emit an unconditional
    ///     <see cref="OpCode.Jump"/> to skip the else-block, patch the false-jump to the
    ///     else-block start, emit the else-block (which may itself be another
    ///     <c>IfStmt</c> for <c>else if</c> chains), then patch the exit jump.</description></item>
    ///   <item><description>If there is no else clause, patch the false-jump immediately
    ///     after the then-block.</description></item>
    /// </list>
    /// <para>
    /// <c>else if</c> chains are nested <see cref="IfStmt"/> nodes in the
    /// <see cref="IfStmt.Else"/> field.  The recursive visit call handles
    /// them naturally; each arm contributes its own pair of jumps.
    /// </para>
    /// </remarks>
    public override object? VisitIf(IfStmt node) {
        int line = node.Range.Start.Line;

        // Emit condition; JumpIfFalse pops and jumps when false.
        Visit(node.Condition);
        int falseJump = EmitJump(OpCode.JumpIfFalse, line);

        // Then-block.
        Visit(node.Then);

        if (node.Else is not null) {
            // Unconditional jump at the end of the then-block to skip the else-block.
            int exitJump = EmitJump(OpCode.Jump, line);

            // Patch the false-jump to land here (start of else-block).
            PatchJump(falseJump);

            // Else-block or nested IfStmt (else if).
            Visit(node.Else);

            // Patch the exit jump to land here (past the else-block).
            PatchJump(exitJump);
        } else {
            // No else: patch the false-jump to land immediately after the then-block.
            PatchJump(falseJump);
        }

        return null;
    }
}
