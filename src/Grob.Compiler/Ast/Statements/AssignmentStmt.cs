using Grob.Core;

namespace Grob.Compiler.Ast.Statements;

/// <summary>A plain assignment statement — <c>target = value</c>.</summary>
/// <param name="Range">Source range covered by the assignment.</param>
/// <param name="Target">The assignment target — identifier, index, or member access.</param>
/// <param name="Value">The value being assigned.</param>
public sealed record AssignmentStmt(
    SourceRange Range,
    Expression Target,
    Expression Value) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitAssignment(this);
}
