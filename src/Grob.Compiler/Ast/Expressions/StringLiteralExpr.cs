using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A double-quoted string literal with no <c>${ ... }</c> interpolations.
/// Interpolated strings use <see cref="InterpolatedStringExpr"/>.
/// </summary>
/// <param name="Range">Source range covered by the literal, including the delimiters.</param>
/// <param name="Value">The decoded string value (escape sequences resolved).</param>
public sealed record StringLiteralExpr(SourceRange Range, string Value) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitStringLiteral(this);
}
