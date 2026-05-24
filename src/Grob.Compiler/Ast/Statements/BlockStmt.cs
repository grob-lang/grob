using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A brace-delimited block of statements introducing a new lexical scope.</summary>
/// <param name="Range">Source range covered by the block, including the braces.</param>
/// <param name="Statements">The statements in source order. May be empty.</param>
public sealed record BlockStmt(
    SourceRange Range,
    IReadOnlyList<Statement> Statements) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitBlock(this);
}
