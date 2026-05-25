using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>Boolean literal — <c>true</c> or <c>false</c>.</summary>
/// <param name="Range">Source range covered by the literal.</param>
/// <param name="Value">The boolean value.</param>
public sealed record BoolLiteralExpr(SourceRange Range, bool Value) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitBoolLiteral(this);
}
