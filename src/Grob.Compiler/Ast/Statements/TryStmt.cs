using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A <c>try</c>/<c>catch</c>/<c>finally</c> statement.</summary>
/// <param name="Range">Source range covered by the whole statement.</param>
/// <param name="Body">The protected block.</param>
/// <param name="Catches">The catch clauses in source order. May be empty when a <see cref="Finally"/> is present.</param>
/// <param name="Finally">The optional <c>finally</c> block.</param>
public sealed record TryStmt(
    SourceRange Range,
    BlockStmt Body,
    IReadOnlyList<CatchClause> Catches,
    BlockStmt? Finally) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitTry(this);
}
