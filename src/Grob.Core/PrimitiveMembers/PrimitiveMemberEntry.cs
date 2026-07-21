namespace Grob.Core.PrimitiveMembers;

/// <summary>
/// A declarative registration for one primitive <see cref="GrobType"/>'s instance
/// surface — the properties and methods <c>Grob.Compiler</c>'s
/// <c>ResolveMemberAccessCall</c>/<c>VisitMemberAccess</c> consult to rewrite a
/// primitive-receiver member access to a qualified native call at compile time
/// (D-066). Parallel to <see cref="NamedTypes.NamedTypeEntry"/>, not an entry within
/// it: a primitive is never <see cref="GrobValueKind.Struct"/>-discriminated, so it has
/// no <c>ToStringRenderer</c> (no <c>ValueDisplay</c> hook — primitive rendering is
/// unconditional) and no runtime <c>Bind</c> shape — dispatch is a pure compile-time
/// rewrite, never a <c>GetProperty</c> runtime lookup.
/// </summary>
/// <param name="ReceiverType">The primitive <see cref="GrobType"/> this entry governs (e.g. <c>GrobType.String</c>).</param>
/// <param name="Properties">Instance properties, keyed by name.</param>
/// <param name="Methods">Instance methods, keyed by name.</param>
public sealed record PrimitiveMemberEntry(
    GrobType ReceiverType,
    IReadOnlyDictionary<string, PrimitiveMemberProperty> Properties,
    IReadOnlyDictionary<string, PrimitiveMemberMethod> Methods);
