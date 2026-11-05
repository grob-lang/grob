namespace Grob.Runtime;

/// <summary>
/// A registered named type as the <see cref="NativeCatalogue"/> serves it: the
/// definition the plugin supplied, its members keyed by bare name for the checker's
/// and the VM's lookups, and the owning plugin.
/// </summary>
public sealed record NamedTypeSurface(
    NamedTypeDefinition Definition,
    IReadOnlyDictionary<string, NamedTypePropertyBinding> Properties,
    IReadOnlyDictionary<string, NamedTypeMethodBinding> Methods,
    string Owner);
