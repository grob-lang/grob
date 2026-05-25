using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A bare identifier reference — name resolution happens in Sprint 2+.</summary>
/// <param name="Range">Source range covered by the identifier.</param>
/// <param name="Name">The identifier text exactly as it appeared in source.</param>
public sealed record IdentifierExpr(SourceRange Range, string Name) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitIdentifier(this);
}
