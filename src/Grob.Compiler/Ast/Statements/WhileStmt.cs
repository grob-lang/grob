using Grob.Core;

namespace Grob.Compiler.Ast.Statements;

/// <summary>A <c>while</c> loop.</summary>
/// <param name="Range">Source range covered by the whole loop.</param>
/// <param name="Condition">The loop condition evaluated before each iteration.</param>
/// <param name="Body">The loop body.</param>
public sealed record WhileStmt(
    SourceRange Range,
    Expression Condition,
    BlockStmt Body) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitWhile(this);
}
