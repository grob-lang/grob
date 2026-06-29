using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A single field initialiser inside a struct-construction expression.</summary>
/// <param name="Range">Source range covering the whole <c>name: value</c> pair.</param>
/// <param name="Name">The field name as it appears at the construction site.</param>
/// <param name="Value">The supplied value expression.</param>
public sealed record FieldInit(SourceRange Range, string Name, Expression Value);

/// <summary>
/// A struct-construction expression — <c>TypeName { field: value, … }</c>.
/// Produces a value of the named struct type.
/// </summary>
/// <param name="Range">Source range covering the whole expression, from the type name to the closing brace.</param>
/// <param name="TypeName">The name of the type being constructed, as it appears in source.</param>
/// <param name="Fields">The field initialisers supplied at this construction site, in source order.</param>
public sealed record StructConstructionExpr(
    SourceRange Range,
    string TypeName,
    IReadOnlyList<FieldInit> Fields) : Expression(Range) {

    /// <summary>
    /// The <see cref="TypeDecl"/> that declares the type being constructed.
    /// Set by the type checker; <see langword="null"/> before type-checking or when
    /// the type name does not resolve to a known type.
    /// </summary>
    public TypeDecl? ResolvedTypeDecl { get; set; }

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitStructConstruction(this);
}
