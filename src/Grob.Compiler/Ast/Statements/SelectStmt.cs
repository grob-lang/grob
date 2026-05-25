using Grob.Core;

namespace Grob.Compiler.Ast.Statements;

/// <summary>
/// A <c>select</c> statement — first-match dispatch with no fall-through.
/// The default block is optional.
/// </summary>
/// <param name="Range">Source range covered by the whole select.</param>
/// <param name="Subject">The value being matched.</param>
/// <param name="Cases">The case clauses in source order.</param>
/// <param name="Default">The optional default block.</param>
public sealed record SelectStmt(
    SourceRange Range,
    Expression Subject,
    IReadOnlyList<CaseClause> Cases,
    BlockStmt? Default) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitSelect(this);
}
