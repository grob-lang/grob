using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A <c>break</c> statement — exits the innermost enclosing loop.</summary>
/// <param name="Range">Source range covered by the keyword.</param>
public sealed record BreakStmt(SourceRange Range) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitBreak(this);
}
