using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A <c>continue</c> statement — jumps to the next loop iteration.</summary>
/// <param name="Range">Source range covered by the keyword.</param>
public sealed record ContinueStmt(SourceRange Range) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitContinue(this);
}
