using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>An indexed access expression — <c>target[index]</c>.</summary>
/// <param name="Range">Source range covered by the whole expression.</param>
/// <param name="Target">The collection being indexed.</param>
/// <param name="Index">The index expression.</param>
public sealed record IndexExpr(
    SourceRange Range,
    Expression Target,
    Expression Index) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitIndex(this);
}
