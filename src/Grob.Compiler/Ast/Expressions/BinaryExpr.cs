using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A binary expression — arithmetic, comparison, logical, or nil-coalescing.</summary>
/// <param name="Range">Source range covered by the whole expression.</param>
/// <param name="Operator">The binary operator.</param>
/// <param name="Left">The left-hand operand.</param>
/// <param name="Right">The right-hand operand.</param>
public sealed record BinaryExpr(
    SourceRange Range,
    BinaryOperator Operator,
    Expression Left,
    Expression Right) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitBinary(this);
}
