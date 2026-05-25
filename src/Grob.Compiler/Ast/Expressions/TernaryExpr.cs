using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A ternary conditional expression — <c>condition ? then : else</c>.</summary>
/// <param name="Range">Source range covered by the whole expression.</param>
/// <param name="Condition">The boolean condition.</param>
/// <param name="Then">The expression evaluated when the condition is true.</param>
/// <param name="Else">The expression evaluated when the condition is false.</param>
public sealed record TernaryExpr(
    SourceRange Range,
    Expression Condition,
    Expression Then,
    Expression Else) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitTernary(this);
}
