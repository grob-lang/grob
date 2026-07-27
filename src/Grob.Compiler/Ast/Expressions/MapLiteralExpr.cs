using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>
/// A map literal — <c>map&lt;K, V&gt;{ "key": value, … }</c> (D-376). <see cref="TypeArguments"/>
/// carries the parsed <c>map&lt;K, V&gt;</c> type reference so the type checker can resolve its
/// value descriptor via the existing <c>ResolveMapValueDescriptor(TypeRef)</c> unchanged — the
/// same shape a <c>map&lt;K, V&gt;</c> parameter/field/<c>var</c> annotation already produces.
/// </summary>
/// <param name="Range">Source range covering <c>map</c> through the closing <c>}</c>.</param>
/// <param name="TypeArguments">
/// The literal's own <c>map&lt;K, V&gt;</c> type reference (<c>Name</c> is always
/// <c>"map"</c>; <c>TypeArguments</c> holds <c>[K, V]</c>).
/// </param>
/// <param name="Entries">The key/value entries in source order. May be empty.</param>
public sealed record MapLiteralExpr(
    SourceRange Range,
    TypeRef TypeArguments,
    IReadOnlyList<MapEntry> Entries) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitMapLiteral(this);
}
