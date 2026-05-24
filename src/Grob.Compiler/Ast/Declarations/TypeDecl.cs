using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A top-level <c>type</c> declaration.</summary>
/// <param name="Range">Source range covered by the whole declaration.</param>
/// <param name="Name">The type name.</param>
/// <param name="Fields">The declared fields in source order. May be empty.</param>
public sealed record TypeDecl(
    SourceRange Range,
    string Name,
    IReadOnlyList<TypeField> Fields) : Declaration(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitTypeDecl(this);
}
