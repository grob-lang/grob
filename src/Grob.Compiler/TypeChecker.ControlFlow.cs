using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Control flow — visit children; return Unknown (control flow has no type).
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Validates that the condition is <c>bool</c> (E0001) then visits
    /// the then-block and the optional else-block or else-if chain.
    /// </remarks>
    public override GrobType VisitIf(IfStmt node) {
        GrobType condType = Visit(node.Condition);
        if (condType != GrobType.Bool && condType != GrobType.Error) {
            EmitError(ErrorCatalog.E0001,
                $"'if' condition must be 'bool'; found '{TypeName(condType)}'.",
                node.Condition.Range);
        }
        Visit(node.Then);
        if (node.Else is not null) Visit(node.Else);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitWhile(WhileStmt node) {
        Visit(node.Condition);
        Visit(node.Body);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitForIn(ForInStmt node) {
        Visit(node.Iterable);
        Visit(node.Body);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitSelect(SelectStmt node) {
        Visit(node.Subject);
        foreach (CaseClause c in node.Cases) {
            foreach (Expression pattern in c.Patterns) Visit(pattern);
            Visit(c.Body);
        }
        if (node.Default is not null) Visit(node.Default);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    public override GrobType VisitTry(TryStmt node) {
        Visit(node.Body);
        foreach (CatchClause c in node.Catches) Visit(c.Body);
        if (node.Finally is not null) Visit(node.Finally);
        return GrobType.Unknown;
    }
}
