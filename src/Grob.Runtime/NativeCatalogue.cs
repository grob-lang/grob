using System.Diagnostics.CodeAnalysis;

using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The single source of truth for every native registered through the plugin registrar
/// (D-426 Decision 2): the type checker reads signatures from it and the VM binds
/// implementations from it. Immutable once <see cref="NativeCatalogueBuilder.Build"/>
/// returns it; one instance per host, built once and shared by every
/// <c>TypeChecker</c> and the <c>VirtualMachine</c> of that host (D-338).
/// </summary>
/// <remarks>
/// A namespace native's qualified name splits at its first dot: <c>guid.namespaces.dns</c>
/// is member <c>namespaces.dns</c> of namespace <c>guid</c>, the flat-key convention of
/// D-149. Namespaces come only from signature-bearing natives and constants; a primitive
/// member's global (<c>string.trim</c>) does not make <c>string</c> a namespace, so
/// <c>string := 5</c> stays legal.
/// </remarks>
public sealed class NativeCatalogue : IValueToStringRegistry {
    private readonly CatalogueTables _tables;
    private readonly ValueDisplay _display;

    internal NativeCatalogue(CatalogueTables tables) {
        _tables = tables;
        _display = new ValueDisplay(tables.Renderers);
    }

    /// <summary>A catalogue with nothing registered. One shared instance.</summary>
    public static NativeCatalogue Empty { get; } = new NativeCatalogueBuilder().Build();

    /// <summary>Every namespace, in registration order.</summary>
    public IReadOnlyList<string> NamespaceNames => _tables.NamespaceNames;

    /// <summary>Every named type, in registration order.</summary>
    public IReadOnlyList<string> NamedTypeNames => _tables.NamedTypeNames;

    /// <summary>
    /// Every runtime global the VM loads: namespace natives and constants by qualified
    /// name, primitive members under their receiver prefix (<c>string.trim</c>) and bare
    /// built-ins, in registration order.
    /// </summary>
    public IReadOnlyDictionary<string, GrobValue> Globals => _tables.Globals;

    /// <summary><see langword="true"/> when <paramref name="name"/> is a registered namespace.</summary>
    public bool IsNamespace(string name) {
        ArgumentNullException.ThrowIfNull(name);
        return _tables.Natives.ContainsKey(name) || _tables.Constants.ContainsKey(name);
    }

    /// <summary>Looks up the native <paramref name="memberName"/> of <paramref name="namespaceName"/>.</summary>
    public bool TryGetNative(string namespaceName, string memberName, [NotNullWhen(true)] out NativeEntry? entry) {
        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentNullException.ThrowIfNull(memberName);
        entry = null;
        return _tables.Natives.TryGetValue(namespaceName, out IReadOnlyDictionary<string, NativeEntry>? members)
            && members.TryGetValue(memberName, out entry);
    }

    /// <summary>Looks up the constant <paramref name="memberName"/> of <paramref name="namespaceName"/>.</summary>
    public bool TryGetConstant(string namespaceName, string memberName, [NotNullWhen(true)] out ConstantEntry? entry) {
        ArgumentNullException.ThrowIfNull(namespaceName);
        ArgumentNullException.ThrowIfNull(memberName);
        entry = null;
        return _tables.Constants.TryGetValue(namespaceName, out IReadOnlyDictionary<string, ConstantEntry>? members)
            && members.TryGetValue(memberName, out entry);
    }

    /// <summary>Looks up the instance members registered on the primitive <paramref name="receiver"/>.</summary>
    public bool TryGetPrimitiveMembers(GrobType receiver, [NotNullWhen(true)] out PrimitiveMemberSurface? surface) =>
        _tables.Primitives.TryGetValue(receiver, out surface);

    /// <summary>Looks up the named type <paramref name="name"/>.</summary>
    public bool TryGetNamedType(string name, [NotNullWhen(true)] out NamedTypeSurface? surface) {
        ArgumentNullException.ThrowIfNull(name);
        return _tables.NamedTypes.TryGetValue(name, out surface);
    }

    /// <summary>Looks up the bare built-in <paramref name="name"/> (<c>input</c>).</summary>
    public bool TryGetBuiltin(string name, [NotNullWhen(true)] out NativeEntry? entry) {
        ArgumentNullException.ThrowIfNull(name);
        return _tables.Builtins.TryGetValue(name, out entry);
    }

    /// <summary>
    /// Renders <paramref name="value"/> through a <see cref="ValueDisplay"/> over this
    /// catalogue's renderers: the same rendering <c>print()</c> uses, including every
    /// plugin type's registered renderer.
    /// </summary>
    public string RenderValue(GrobValue value) => _display.Display(value);

    bool IValueToStringRegistry.TryToString(GrobValue value, [NotNullWhen(true)] out string? rendered) =>
        _tables.Renderers.TryToString(value, out rendered);
}
