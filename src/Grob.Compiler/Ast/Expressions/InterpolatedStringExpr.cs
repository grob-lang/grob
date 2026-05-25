using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>
/// A double-quoted string with one or more <c>${ ... }</c> interpolations.
/// The parts list alternates between literal text and embedded expressions.
/// </summary>
/// <param name="Range">Source range covered by the literal, including the delimiters.</param>
/// <param name="Parts">The literal-text and embedded-expression segments in source order.</param>
public sealed record InterpolatedStringExpr(
    SourceRange Range,
    IReadOnlyList<StringInterpolationPart> Parts) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitInterpolatedString(this);
}
