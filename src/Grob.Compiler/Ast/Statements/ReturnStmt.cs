using Grob.Core;

namespace Grob.Compiler.Ast.Statements;

/// <summary>A <c>return</c> statement, with optional return value.</summary>
/// <param name="Range">Source range covered by the statement.</param>
/// <param name="Value">The optional return value.</param>
public sealed record ReturnStmt(
    SourceRange Range,
    Expression? Value) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitReturn(this);
}
