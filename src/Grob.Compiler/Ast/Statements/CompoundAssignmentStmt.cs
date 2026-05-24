using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A compound assignment statement — <c>target += value</c> and friends.</summary>
/// <param name="Range">Source range covered by the statement.</param>
/// <param name="Target">The assignment target — identifier, index, or member access.</param>
/// <param name="Operator">The compound operator.</param>
/// <param name="Value">The value combined with the target.</param>
public sealed record CompoundAssignmentStmt(
    SourceRange Range,
    Expression Target,
    CompoundAssignmentOperator Operator,
    Expression Value) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitCompoundAssignment(this);
}
