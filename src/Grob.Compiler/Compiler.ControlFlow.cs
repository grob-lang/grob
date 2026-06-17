using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class Compiler {
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
