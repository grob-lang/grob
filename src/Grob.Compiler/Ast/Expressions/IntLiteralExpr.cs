using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>Integer literal — e.g. <c>42</c>, <c>0xff</c>, <c>1_000</c>.</summary>
/// <param name="Range">Source range covered by the literal.</param>
/// <param name="Value">The decoded integer value.</param>
public sealed record IntLiteralExpr(SourceRange Range, long Value) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitIntLiteral(this);
}
