using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>An embedded <c>${ ... }</c> expression inside an <see cref="InterpolatedStringExpr"/>.</summary>
/// <param name="Range">Source range covered by the expression, including the surrounding <c>${ }</c>.</param>
/// <param name="Expression">The embedded expression.</param>
public sealed record StringExpressionPart(
    SourceRange Range,
    Expression Expression) : StringInterpolationPart(Range);
