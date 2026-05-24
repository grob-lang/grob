using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>Floating-point literal — e.g. <c>1.5</c>, <c>2.0e10</c>.</summary>
/// <param name="Range">Source range covered by the literal.</param>
/// <param name="Value">The decoded floating-point value.</param>
public sealed record FloatLiteralExpr(SourceRange Range, double Value) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitFloatLiteral(this);
}
