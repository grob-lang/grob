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
/// <param name="HadRecoveredArm">
/// <see langword="true"/> when the parser dropped at least one malformed arm during
/// local recovery (D-406). Threaded through so the type checker can suppress a
/// derived, spurious <see cref="ErrorCatalog.E0505"/> non-exhaustiveness diagnostic
/// when the dropped arm might itself have been the one carrying exhaustiveness (the
/// <c>_</c> catch-all, a required <c>bool</c> arm, or the nil arm on a nullable
/// subject) — the same cascade-suppression principle as the <c>subjectType != Error</c>
/// guard immediately alongside it, applied to a parse-time rather than a type-time
/// root cause.
/// </param>
public sealed record SwitchExprNode(
    SourceRange Range,
    Expression Subject,
    IReadOnlyList<SwitchArm> Arms,
    bool HadRecoveredArm = false) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitSwitchExpr(this);
}
