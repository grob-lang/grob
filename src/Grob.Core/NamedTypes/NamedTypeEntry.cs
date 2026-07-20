namespace Grob.Core.NamedTypes;

/// <summary>
/// A declarative registration for one nominal, <see cref="GrobValueKind.Struct"/>-discriminated
/// type's instance surface (D-356) — the properties, methods and display renderer that
/// <c>Grob.Compiler</c>'s annotation resolvers and method/property validators, <c>Grob.Vm</c>'s
/// instance dispatch, and <c>Grob.Runtime</c>'s <c>ValueDisplay</c> all consult in place of
/// per-type, string-matched arms. Governs the instance surface only — a named type's static
/// constructors (<c>date.now()</c>, <c>guid.parse()</c>) stay <c>NamespaceRegistry</c> entries;
/// the two registries compose.
/// </summary>
/// <param name="CanonicalName">
/// The type name as it appears in an annotation and as <see cref="GrobStruct.TypeName"/> carries
/// it at runtime (e.g. <c>"guid"</c>, <c>"date"</c>).
/// </param>
/// <param name="Properties">Instance properties, keyed by name.</param>
/// <param name="Methods">Instance methods, keyed by name.</param>
/// <param name="ToStringRenderer">
/// The <c>ValueDisplay</c> (D-336) renderer — registered by the owning plugin ahead of the
/// structural <c>Struct</c> rendering fallback, preserving the credential-ordering guarantee.
/// </param>
public sealed record NamedTypeEntry(
    string CanonicalName,
    IReadOnlyDictionary<string, NamedTypeProperty> Properties,
    IReadOnlyDictionary<string, NamedTypeMethod> Methods,
    Func<GrobValue, string> ToStringRenderer);
