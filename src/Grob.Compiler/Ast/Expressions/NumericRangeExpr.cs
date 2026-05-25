using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>
/// A numeric range expression — <c>start..end</c> with optional <c>step</c>.
/// Currently only valid as the iterable of a <see cref="ForInStmt"/>; surfacing
/// it as a regular <see cref="Expression"/> keeps the for-in shape uniform.
/// </summary>
/// <param name="Range">Source range covered by the whole range expression.</param>
/// <param name="Start">The inclusive start bound.</param>
/// <param name="End">The inclusive end bound.</param>
/// <param name="Step">The optional <c>step</c> expression.</param>
public sealed record NumericRangeExpr(
    SourceRange Range,
    Expression Start,
    Expression End,
    Expression? Step) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitNumericRange(this);
}
