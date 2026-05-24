using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>An <c>x++</c> or <c>x--</c> statement.</summary>
/// <param name="Range">Source range covered by the statement.</param>
/// <param name="Target">The target being incremented or decremented.</param>
/// <param name="Kind">Whether the operator is <c>++</c> or <c>--</c>.</param>
public sealed record IncrementStmt(
    SourceRange Range,
    Expression Target,
    IncrementKind Kind) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitIncrement(this);
}
