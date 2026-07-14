using System.Diagnostics.CodeAnalysis;

using Grob.Core;
using Grob.Runtime;

namespace Grob.Vm;

/// <summary>
/// The real <see cref="IValueToStringRegistry"/> implementation (Sprint 8 Increment D —
/// the "later increment" <c>NullRegistry</c>'s doc comments named). Backs
/// <see cref="VirtualMachine.RegisterToString"/>: a plugin-owned type
/// (<c>guid</c>) registers its <c>toString()</c> here, keyed by the
/// <see cref="GrobStruct.TypeName"/> its values carry, so <see cref="ValueDisplay"/>'s
/// step-2 registry lookup renders it instead of falling through to structural
/// rendering (D-336).
/// </summary>
internal sealed class PluginToStringRegistry : IValueToStringRegistry {
    private readonly Dictionary<string, Func<GrobValue, string>> _renderers = new(StringComparer.Ordinal);

    /// <summary>Registers <paramref name="toString"/> for every <c>Struct</c> value named <paramref name="typeName"/>.</summary>
    internal void Register(string typeName, Func<GrobValue, string> toString) {
        ArgumentNullException.ThrowIfNull(typeName);
        ArgumentNullException.ThrowIfNull(toString);
        _renderers[typeName] = toString;
    }

    /// <inheritdoc/>
    public bool TryToString(GrobValue value, [NotNullWhen(true)] out string? rendered) {
        if (value.TryAsStruct(out GrobStruct? s) && _renderers.TryGetValue(s!.TypeName, out Func<GrobValue, string>? fn)) {
            rendered = fn(value);
            return true;
        }
        rendered = null;
        return false;
    }
}
