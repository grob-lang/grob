using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>
/// A switch expression — <c>subject switch { pattern =&gt; result, _ =&gt; default }</c>
/// (§3.1). The exhaustive, value-producing counterpart to the <c>select</c> statement
/// (D-301): the type checker proves exhaustiveness and unifies every arm result to the
/// node's single <c>ResolvedType</c>.
/// </summary>
/// <param name="Range">Source range from the subject to the closing brace.</param>
/// <param name="Subject">The scrutinee evaluated once and tested against each arm.</param>
/// <param name="Arms">The arms, tested in source order; the first match wins.</param>
public sealed record SwitchExprNode(
    SourceRange Range,
    Expression Subject,
    IReadOnlyList<SwitchArm> Arms) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitSwitchExpr(this);
}
