using Grob.Core;

namespace Grob.Compiler.Ast.Statements;

/// <summary>
/// An expression used in statement position — typically a call to a built-in
/// such as <c>print(x)</c> or <c>exit()</c>, which the v1 spec models as
/// ordinary functions rather than statement keywords.
/// </summary>
/// <param name="Range">Source range covered by the expression.</param>
/// <param name="Expression">The expression evaluated for its effect.</param>
public sealed record ExpressionStmt(
    SourceRange Range,
    Expression Expression) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitExpressionStmt(this);
}
