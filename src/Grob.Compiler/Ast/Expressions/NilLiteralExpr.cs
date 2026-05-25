using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>The <c>nil</c> literal.</summary>
/// <param name="Range">Source range covered by the literal.</param>
public sealed record NilLiteralExpr(SourceRange Range) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitNilLiteral(this);
}
