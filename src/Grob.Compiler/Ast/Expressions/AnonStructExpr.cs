using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>
/// An anonymous-struct literal — <c>#{ field: value, … }</c>.
/// Produces a value whose type the type checker synthesises from the literal's
/// field names and value types (Sprint 6D, §10 brace-disambiguation rule).
/// </summary>
/// <param name="Range">Source range covering <c>#{</c> through the closing <c>}</c>.</param>
/// <param name="Fields">Field initialisers in source order.</param>
public sealed record AnonStructExpr(
    SourceRange Range,
    IReadOnlyList<FieldInit> Fields) : Expression(Range) {

    /// <summary>
    /// The canonical structural-type signature synthesised by the type checker.
    /// Format: sorted <c>"field:GrobType"</c> pairs joined by commas.
    /// <see langword="null"/> before type-checking.
    /// </summary>
    public string? SynthesisedTypeName { get; set; }

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitAnonStruct(this);
}
