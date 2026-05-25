using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A prefix unary expression — <c>-x</c> or <c>!x</c>.</summary>
/// <param name="Range">Source range covered by the operator and operand together.</param>
/// <param name="Operator">The unary operator.</param>
/// <param name="Operand">The operand expression.</param>
public sealed record UnaryExpr(
    SourceRange Range,
    UnaryOperator Operator,
    Expression Operand) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitUnary(this);
}
