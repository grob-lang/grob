using Grob.Core;

namespace Grob.Compiler.Ast.Declarations;

/// <summary>A top-level <c>readonly</c> declaration.</summary>
/// <param name="Range">Source range covered by the declaration.</param>
/// <param name="Name">The bound name.</param>
/// <param name="AnnotatedType">The optional declared type.</param>
/// <param name="Value">The right-hand-side expression. Any expression is legal — evaluated at declaration site.</param>
public sealed record ReadonlyDecl(
    SourceRange Range,
    string Name,
    TypeRef? AnnotatedType,
    Expression Value) : Declaration(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitReadonlyDecl(this);
}
