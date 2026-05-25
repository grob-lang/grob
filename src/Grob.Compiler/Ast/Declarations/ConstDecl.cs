using Grob.Core;

namespace Grob.Compiler.Ast.Declarations;

/// <summary>A top-level <c>const</c> declaration.</summary>
/// <param name="Range">Source range covered by the declaration.</param>
/// <param name="Name">The bound name.</param>
/// <param name="AnnotatedType">The optional declared type.</param>
/// <param name="Value">The right-hand-side expression. Must be a compile-time constant per §24.</param>
public sealed record ConstDecl(
    SourceRange Range,
    string Name,
    TypeRef? AnnotatedType,
    Expression Value) : Declaration(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitConstDecl(this);
}
