using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>An array literal — <c>[a, b, c]</c>.</summary>
/// <param name="Range">Source range covered by the literal, including the brackets.</param>
/// <param name="Elements">The element expressions in source order. May be empty.</param>
public sealed record ArrayLiteralExpr(
    SourceRange Range,
    IReadOnlyList<Expression> Elements) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitArrayLiteral(this);
}
