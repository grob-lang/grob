using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class Compiler {
    // -----------------------------------------------------------------------
    // Block
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitBlock(BlockStmt node) {
        foreach (Statement stmt in node.Statements) {
            Visit(stmt);
        }
        return null;
    }

    // -----------------------------------------------------------------------
    // Expression statements (print, exit, and other calls)
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public override object? VisitExpressionStmt(ExpressionStmt node) {
        if (node.Expression is CallExpr call &&
            call.Callee is IdentifierExpr { Name: "print" } &&
            call.Arguments.Count == 1) {
            // Emit the single argument then the Print opcode.
            Visit(call.Arguments[0].Value);
            _chunk.WriteOpCode(OpCode.Print, call.Range.Start.Line);
            return null;
        }

        // For all other expression statements, emit the expression value.
        // It remains on the stack (Sprint 2 has no Pop opcode); this is fine for
        // bare-expression tests and is not observable at the user level.
        Visit(node.Expression);
        return null;
    }
}
