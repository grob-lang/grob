using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// An <c>if</c> statement with optional else branch. The else branch is itself
/// a <see cref="Statement"/> so that <c>else if</c> chains are represented as
/// nested <see cref="IfStmt"/> nodes and a final <c>else</c> as a
/// <see cref="BlockStmt"/>.
/// </summary>
/// <param name="Range">Source range covered by the whole if statement.</param>
/// <param name="Condition">The condition expression.</param>
/// <param name="Then">The then-branch block.</param>
/// <param name="Else">The else branch — another <see cref="IfStmt"/>, a <see cref="BlockStmt"/>, or <see langword="null"/>.</param>
public sealed record IfStmt(
    SourceRange Range,
    Expression Condition,
    BlockStmt Then,
    Statement? Else) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitIf(this);
}
