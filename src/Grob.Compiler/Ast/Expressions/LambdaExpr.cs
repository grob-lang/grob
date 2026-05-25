using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>
/// A lambda expression — <c>x =&gt; expr</c>, <c>x =&gt; { ... }</c>,
/// <c>(a, b) =&gt; expr</c>.
/// </summary>
/// <param name="Range">Source range covered by the whole lambda.</param>
/// <param name="Parameters">The lambda parameters.</param>
/// <param name="Body">The lambda body — expression or block.</param>
public sealed record LambdaExpr(
    SourceRange Range,
    IReadOnlyList<Parameter> Parameters,
    LambdaBody Body) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitLambda(this);
}
