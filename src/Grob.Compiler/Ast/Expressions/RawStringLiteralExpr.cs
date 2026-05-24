using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A raw backtick string literal — no escape processing, no interpolation.</summary>
/// <param name="Range">Source range covered by the literal, including the backticks.</param>
/// <param name="Value">The raw string value verbatim from source.</param>
public sealed record RawStringLiteralExpr(SourceRange Range, string Value) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitRawStringLiteral(this);
}
