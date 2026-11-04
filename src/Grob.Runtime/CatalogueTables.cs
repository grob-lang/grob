using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The frozen tables a <see cref="NativeCatalogueBuilder"/> hands a
/// <see cref="NativeCatalogue"/>: one object rather than a ten-parameter constructor.
/// Every collection is read-only by the time it arrives here.
/// </summary>
internal sealed class CatalogueTables {
    /// <summary>Every namespace, in registration order.</summary>
    public required IReadOnlyList<string> NamespaceNames { get; init; }

    /// <summary>Namespace natives by namespace, then by member name.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, NativeEntry>> Natives { get; init; }

    /// <summary>Namespace constants by namespace, then by member name.</summary>
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<string, ConstantEntry>> Constants { get; init; }

    /// <summary>Primitive member surfaces by receiver.</summary>
    public required IReadOnlyDictionary<GrobType, PrimitiveMemberSurface> Primitives { get; init; }

    /// <summary>Every named type, in registration order.</summary>
    public required IReadOnlyList<string> NamedTypeNames { get; init; }

    /// <summary>Named types by name.</summary>
    public required IReadOnlyDictionary<string, NamedTypeSurface> NamedTypes { get; init; }

    /// <summary>Bare built-ins by name.</summary>
    public required IReadOnlyDictionary<string, NativeEntry> Builtins { get; init; }

    /// <summary>Every runtime global, in registration order.</summary>
    public required IReadOnlyDictionary<string, GrobValue> Globals { get; init; }

    /// <summary>The renderer table the builder filled.</summary>
    public required RendererTable Renderers { get; init; }
}
