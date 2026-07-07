using Grob.Core;

namespace Grob.Compiler.Ast.Statements;

/// <summary>
/// A <c>throw</c> statement (D-274, §27). The operand is mandatory — unlike
/// <see cref="ReturnStmt"/>, there is no bare-<c>throw</c> form.
/// </summary>
/// <param name="Range">Source range covered by the statement.</param>
/// <param name="Value">The thrown expression. Must evaluate to a <c>GrobError</c> subtype (E0014).</param>
public sealed record ThrowStmt(
    SourceRange Range,
    Expression Value) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitThrow(this);
}
