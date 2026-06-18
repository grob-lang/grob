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
    /// <remarks>
    /// Validates that the condition is <c>bool</c> (E0001), then walks the body
    /// with <see cref="_loopDepth"/> incremented so that <c>break</c> and
    /// <c>continue</c> inside the body are accepted.
    /// </remarks>
    public override GrobType VisitWhile(WhileStmt node) {
        GrobType condType = Visit(node.Condition);
        if (condType != GrobType.Bool && condType != GrobType.Error) {
            EmitError(ErrorCatalog.E0001,
                $"'while' condition must be 'bool'; found '{TypeName(condType)}'.",
                node.Condition.Range);
        }
        _loopDepth++;
        Visit(node.Body);
        _loopDepth--;
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validates that <c>break</c> appears inside a loop (<see cref="_loopDepth"/> &gt; 0).
    /// A <c>break</c> inside a <c>select</c> case that is itself inside a loop is valid
    /// because <c>select</c> (Increment D) does not push to <see cref="_loopDepth"/>.
    /// </remarks>
    public override GrobType VisitBreak(BreakStmt node) {
        if (_loopDepth == 0)
            EmitError(ErrorCatalog.E2211, "'break' used outside a loop.", node.Range);
        return GrobType.Unknown;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Validates that <c>continue</c> appears inside a loop (<see cref="_loopDepth"/> &gt; 0).
    /// A <c>continue</c> inside a <c>select</c> case that is itself inside a loop is valid
    /// because <c>select</c> (Increment D) does not push to <see cref="_loopDepth"/>.
    /// </remarks>
    public override GrobType VisitContinue(ContinueStmt node) {
        if (_loopDepth == 0)
            EmitError(ErrorCatalog.E2212, "'continue' used outside a loop.", node.Range);
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
