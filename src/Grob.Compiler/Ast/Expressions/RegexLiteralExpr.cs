using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A regex literal — e.g. <c>/foo.*bar/gi</c>.</summary>
/// <param name="Range">Source range covered by the literal, including the delimiters.</param>
/// <param name="Pattern">The regex pattern text between the slashes.</param>
/// <param name="Flags">The flags suffix (may be empty).</param>
public sealed record RegexLiteralExpr(
    SourceRange Range,
    string Pattern,
    string Flags) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitRegexLiteral(this);
}
