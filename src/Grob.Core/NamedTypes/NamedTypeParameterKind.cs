namespace Grob.Core.NamedTypes;

/// <summary>
/// How a <see cref="NamedTypeMethod"/> parameter's declared <see cref="GrobType"/>
/// is checked against a call-site argument.
/// </summary>
public enum NamedTypeParameterKind {
    /// <summary>The argument must be assignable to the declared primitive/flat <see cref="GrobType"/>.</summary>
    Primitive,

    /// <summary>
    /// The argument must be a <see cref="GrobType.Struct"/> whose nominal name is the
    /// entry's own <see cref="NamedTypeEntry.CanonicalName"/> — the flat
    /// <see cref="GrobType.Struct"/> tag is shared by every nominal type, so this is
    /// a same-type identity check, not merely "any struct" (e.g. <c>date.isBefore(other: date)</c>).
    /// </summary>
    NominalSelf,
}
