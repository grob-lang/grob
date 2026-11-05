using System.Diagnostics.CodeAnalysis;

using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The renderer table a <see cref="NativeCatalogueBuilder"/> fills and its
/// <see cref="NativeCatalogue"/> keeps (D-336, gate report §1.3). One instance serves
/// both, so a <c>RenderValue</c> delegate captured during registration keeps rendering
/// through the final set of renderers. Keyed by <see cref="GrobStruct.TypeName"/>.
/// </summary>
internal sealed class RendererTable : IValueToStringRegistry {
    private readonly Dictionary<string, (Func<GrobValue, string> Render, string Owner)> _renderers = new(StringComparer.Ordinal);

    /// <summary>
    /// Adds a renderer. Returns <see langword="false"/> and the existing owner when
    /// <paramref name="typeName"/> already has one.
    /// </summary>
    internal bool TryAdd(string typeName, Func<GrobValue, string> render, string owner, [NotNullWhen(false)] out string? existingOwner) {
        if (_renderers.TryGetValue(typeName, out (Func<GrobValue, string> Render, string Owner) existing)) {
            existingOwner = existing.Owner;
            return false;
        }
        _renderers[typeName] = (render, owner);
        existingOwner = null;
        return true;
    }

    /// <inheritdoc/>
    public bool TryToString(GrobValue value, [NotNullWhen(true)] out string? rendered) {
        if (value.TryAsStruct(out GrobStruct? s)
                && _renderers.TryGetValue(s!.TypeName, out (Func<GrobValue, string> Render, string Owner) entry)) {
            rendered = entry.Render(value);
            return true;
        }
        rendered = null;
        return false;
    }
}
