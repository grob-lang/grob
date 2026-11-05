using System.Diagnostics.CodeAnalysis;

using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The renderer table a <see cref="NativeCatalogueBuilder"/> fills and its
/// <see cref="NativeCatalogue"/> keeps (D-336, gate report §1.3). One instance serves
/// both, so a <c>RenderValue</c> delegate captured during registration keeps rendering
/// through the final set of renderers. Keyed by <see cref="GrobStruct.TypeName"/>; a
/// renderer arrives only with its named type, whose registration already rejects a
/// duplicate name, so there is no second guard here.
/// </summary>
internal sealed class RendererTable : IValueToStringRegistry {
    private readonly Dictionary<string, Func<GrobValue, string>> _renderers = new(StringComparer.Ordinal);

    /// <summary>Adds the renderer for the named type <paramref name="typeName"/>.</summary>
    internal void Add(string typeName, Func<GrobValue, string> render) => _renderers.Add(typeName, render);

    /// <inheritdoc/>
    public bool TryToString(GrobValue value, [NotNullWhen(true)] out string? rendered) {
        if (value.TryAsStruct(out GrobStruct? s) && _renderers.TryGetValue(s!.TypeName, out Func<GrobValue, string>? render)) {
            rendered = render(value);
            return true;
        }
        rendered = null;
        return false;
    }
}
