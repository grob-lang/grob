using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using Grob.Core;
using Grob.Runtime;

namespace Grob.Compiler;

/// <summary>
/// The type checker's view of the namespaces one <see cref="NativeCatalogue"/> declares
/// (D-426 Decision 2): the namespace names, the shared <see cref="NamespaceDecl"/> and
/// <see cref="Symbol"/> per name, and member lookup. Built once per catalogue and cached
/// by catalogue identity, so a compile allocates none of it (D-338).
/// </summary>
/// <remarks>
/// Until change group 5 migrates the namespace natives, the catalogue holds only typed
/// registrations and constants, so lookup falls back to <see cref="NamespaceRegistry"/>'s
/// hand table for anything the catalogue does not hold, and the names are the union of
/// both. The catalogue wins where both declare a member.
/// </remarks>
internal sealed class NamespaceSymbols {
    private static readonly ConditionalWeakTable<NativeCatalogue, NamespaceSymbols> _cache = new();

    private readonly NativeCatalogue _catalogue;
    private readonly Dictionary<string, Symbol> _symbols;

    private NamespaceSymbols(NativeCatalogue catalogue) {
        _catalogue = catalogue;
        Names = [.. catalogue.NamespaceNames.Union(NamespaceRegistry.NamespaceNames, StringComparer.Ordinal)];
        _symbols = new Dictionary<string, Symbol>(Names.Length, StringComparer.Ordinal);
        foreach (string name in Names) {
            _symbols[name] = new Symbol {
                Name = name,
                Type = GrobType.Unknown,
                DeclaredAt = SourceLocation.Unknown,
                DeclarationNode = new NamespaceDecl(name),
            };
        }
    }

    /// <summary>The cached view of <paramref name="catalogue"/>, built on first request.</summary>
    internal static NamespaceSymbols For(NativeCatalogue catalogue) =>
        _cache.GetValue(catalogue, static c => new NamespaceSymbols(c));

    /// <summary>
    /// Every namespace name. A concrete array, not <c>IEnumerable&lt;string&gt;</c>, so the
    /// per-compile <c>foreach</c> in <c>RegisterNamespaces</c> does not box an enumerator.
    /// </summary>
    internal string[] Names { get; }

    /// <summary>The shared <see cref="Symbol"/> for <paramref name="name"/>.</summary>
    internal Symbol SymbolFor(string name) => _symbols[name];

    /// <summary>Looks up the native <paramref name="memberName"/> of <paramref name="namespaceName"/>.</summary>
    internal bool TryGetNative(string namespaceName, string memberName, [NotNullWhen(true)] out FunctionSignature? signature) {
        if (_catalogue.TryGetNative(namespaceName, memberName, out NativeEntry? entry)) {
            signature = entry.Signature;
            return true;
        }
        return NamespaceRegistry.TryGetNativeSignature(namespaceName, memberName, out signature);
    }

    /// <summary>Looks up the type of the constant <paramref name="memberName"/> of <paramref name="namespaceName"/>.</summary>
    internal bool TryGetConstant(string namespaceName, string memberName, [NotNullWhen(true)] out SignatureType.Concrete? type) {
        if (_catalogue.TryGetConstant(namespaceName, memberName, out ConstantEntry? entry)) {
            type = entry.Type;
            return true;
        }
        return NamespaceRegistry.TryGetConstantType(namespaceName, memberName, out type);
    }

    /// <summary><see langword="true"/> when <paramref name="namespaceName"/> declares <paramref name="memberName"/>.</summary>
    internal bool HasMember(string namespaceName, string memberName) =>
        TryGetNative(namespaceName, memberName, out _) || TryGetConstant(namespaceName, memberName, out _);
}
