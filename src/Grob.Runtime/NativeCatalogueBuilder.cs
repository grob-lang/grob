using System.Collections.ObjectModel;

using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// Runs plugin registration once and produces the <see cref="NativeCatalogue"/> (D-426
/// Decision 2). Each <see cref="Add"/> runs the plugin's <see cref="IGrobPlugin.Register"/>
/// against a registrar scoped to that plugin, so every entry records its owner, and then
/// <see cref="IPrimitiveMemberPlugin.RegisterPrimitiveMembers"/> when the plugin is
/// first-party. Registration invariants are host errors thrown as
/// <see cref="InvalidOperationException"/> naming the member and both owners:
/// a runtime global registered twice on any surface, a namespace or named type touched
/// by a plugin other than the one that first registered it, and a primitive type name
/// claimed as a namespace from the public surface (gate report F8). Validation precedes
/// every write, so a rejected registration leaves no trace. A renderer arrives only with
/// its named type, so the named-type guard is the renderer guard too.
/// </summary>
public sealed class NativeCatalogueBuilder {
    private readonly Dictionary<string, GrobValue> _globals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _globalOwners = new(StringComparer.Ordinal);
    private readonly List<string> _namespaceNames = [];
    private readonly Dictionary<string, string> _namespaceOwners = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, NativeEntry>> _natives = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, ConstantEntry>> _constants = new(StringComparer.Ordinal);
    private readonly Dictionary<GrobType, PrimitiveTables> _primitives = [];
    private readonly List<string> _namedTypeNames = [];
    private readonly Dictionary<string, NamedTypeSurface> _namedTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, NativeEntry> _builtins = new(StringComparer.Ordinal);
    private readonly RendererTable _renderers = new();
    private readonly ValueDisplay _display;
    private bool _built;

    /// <summary>Creates an empty builder.</summary>
    public NativeCatalogueBuilder() {
        _display = new ValueDisplay(_renderers);
    }

    /// <summary>
    /// Registers <paramref name="plugin"/>. Throws <see cref="ArgumentException"/> when the
    /// plugin has no name and <see cref="InvalidOperationException"/> when the catalogue
    /// is already built or a registration invariant fails. Returns this builder.
    /// </summary>
    public NativeCatalogueBuilder Add(IGrobPlugin plugin) {
        ArgumentNullException.ThrowIfNull(plugin);
        if (string.IsNullOrEmpty(plugin.Name)) {
            throw new ArgumentException("A plugin must have a name.", nameof(plugin));
        }
        RequireNotBuilt();
        var registrar = new PluginRegistrar(this, plugin.Name);
        plugin.Register(registrar);
        if (plugin is IPrimitiveMemberPlugin primitivePlugin) {
            primitivePlugin.RegisterPrimitiveMembers(registrar);
        }
        return this;
    }

    /// <summary>Freezes the registrations into a catalogue. Callable once.</summary>
    public NativeCatalogue Build() {
        RequireNotBuilt();
        _built = true;
        return new NativeCatalogue(new CatalogueTables {
            NamespaceNames = [.. _namespaceNames],
            Natives = Freeze(_natives),
            Constants = Freeze(_constants),
            Primitives = new ReadOnlyDictionary<GrobType, PrimitiveMemberSurface>(
                _primitives.ToDictionary(p => p.Key, p => p.Value.ToSurface(p.Key))),
            NamedTypeNames = [.. _namedTypeNames],
            NamedTypes = new ReadOnlyDictionary<string, NamedTypeSurface>(_namedTypes),
            Builtins = new ReadOnlyDictionary<string, NativeEntry>(_builtins),
            Globals = new ReadOnlyDictionary<string, GrobValue>(_globals),
            Renderers = _renderers,
        });
    }

    private static ReadOnlyDictionary<string, IReadOnlyDictionary<string, T>> Freeze<T>(
            Dictionary<string, Dictionary<string, T>> namespaces) =>
        new(namespaces.ToDictionary(
            ns => ns.Key,
            ns => (IReadOnlyDictionary<string, T>)new ReadOnlyDictionary<string, T>(ns.Value),
            StringComparer.Ordinal));

    private void RequireNotBuilt() {
        if (_built) {
            throw new InvalidOperationException("The catalogue has already been built; nothing more can be added to it.");
        }
    }

    // -----------------------------------------------------------------------
    // Public registrar surface, scoped to one owner.
    // -----------------------------------------------------------------------

    private void RegisterNative(string owner, FunctionSignature signature, NativeFunction implementation) {
        RequireNotBuilt();
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(implementation);
        (string namespaceName, string memberName) = SplitQualified(signature.Name, nameof(signature));
        RequireNotReserved(namespaceName, owner);
        AddNamespaceNative(owner, namespaceName, memberName, signature, implementation);
    }

    private void RegisterUntypedNative(string owner, string name, NativeFunction implementation) {
        RequireNotBuilt();
        Identifiers.RequireQualifiedName(name, nameof(name), "A native name");
        ArgumentNullException.ThrowIfNull(implementation);
        ClaimGlobal(owner, name, GrobValue.FromFunction(implementation));
    }

    private void RegisterConstant(string owner, string name, GrobValue value) {
        RequireNotBuilt();
        (string namespaceName, string memberName) = SplitQualified(name, nameof(name));
        SignatureType.Concrete type = DeriveConstantType(value);
        RequireNotReserved(namespaceName, owner);
        RequireNamespaceOwner(owner, namespaceName, name);
        ClaimGlobal(owner, name, value);
        ClaimNamespace(owner, namespaceName);
        Members(_constants, namespaceName)[memberName] = new ConstantEntry(name, type, value, owner);
    }

    private void RegisterNamedType(string owner, NamedTypeDefinition definition) {
        RequireNotBuilt();
        ArgumentNullException.ThrowIfNull(definition);
        if (_namedTypes.TryGetValue(definition.Name, out NamedTypeSurface? existing)) {
            throw new InvalidOperationException(
                $"Named type '{definition.Name}' is already registered by plugin '{existing.Owner}'; plugin '{owner}' cannot register it again.");
        }
        _renderers.Add(definition.Name, definition.Render);
        _namedTypeNames.Add(definition.Name);
        _namedTypes[definition.Name] = new NamedTypeSurface(
            definition,
            new ReadOnlyDictionary<string, NamedTypePropertyBinding>(
                definition.Properties.ToDictionary(p => p.Signature.Name, StringComparer.Ordinal)),
            new ReadOnlyDictionary<string, NamedTypeMethodBinding>(
                definition.Methods.ToDictionary(m => m.Signature.Name, StringComparer.Ordinal)),
            owner);
    }

    // -----------------------------------------------------------------------
    // Internal primitive surface, first-party only (D-426 Decision 3).
    // -----------------------------------------------------------------------

    private void RegisterPrimitiveProperty(string owner, GrobType receiver, PropertySignature signature, NativeFunction implementation) {
        RequireNotBuilt();
        string prefix = PrimitiveReceivers.NameOf(receiver, nameof(receiver));
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(implementation);
        string qualifiedName = prefix + "." + signature.Name;
        ClaimGlobal(owner, qualifiedName, GrobValue.FromFunction(implementation));
        Tables(receiver).Properties[signature.Name] = new PrimitivePropertyEntry(signature, qualifiedName, implementation);
    }

    private void RegisterPrimitiveMethod(string owner, GrobType receiver, FunctionSignature signature, NativeFunction implementation) {
        RequireNotBuilt();
        string prefix = PrimitiveReceivers.NameOf(receiver, nameof(receiver));
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(implementation);
        RequireBare(signature, "A primitive method");
        string qualifiedName = prefix + "." + signature.Name;
        ClaimGlobal(owner, qualifiedName, GrobValue.FromFunction(implementation));
        Tables(receiver).Methods[signature.Name] = new PrimitiveMethodEntry(signature, qualifiedName, implementation);
    }

    private void RegisterPrimitiveStatic(string owner, FunctionSignature signature, NativeFunction implementation) {
        RequireNotBuilt();
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(implementation);
        (string namespaceName, string memberName) = SplitQualified(signature.Name, nameof(signature));
        if (!PrimitiveReceivers.IsReserved(namespaceName)) {
            throw new ArgumentException(
                $"'{namespaceName}' is not a primitive type name; only int, float, string and bool type-statics register here.",
                nameof(signature));
        }
        AddNamespaceNative(owner, namespaceName, memberName, signature, implementation);
    }

    private void RegisterBuiltin(string owner, FunctionSignature signature, NativeFunction implementation) {
        RequireNotBuilt();
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(implementation);
        RequireBare(signature, "A built-in");
        ClaimGlobal(owner, signature.Name, GrobValue.FromFunction(implementation));
        _builtins[signature.Name] = new NativeEntry(signature, implementation, owner);
    }

    private static void RequireBare(FunctionSignature signature, string what) {
        if (signature.Name.Contains('.')) {
            throw new ArgumentException($"{what} must have a bare name; '{signature.Name}' is qualified.", nameof(signature));
        }
    }

    private PrimitiveTables Tables(GrobType receiver) {
        if (!_primitives.TryGetValue(receiver, out PrimitiveTables? tables)) {
            tables = new PrimitiveTables();
            _primitives[receiver] = tables;
        }
        return tables;
    }

    // -----------------------------------------------------------------------
    // Shared mechanics.
    // -----------------------------------------------------------------------

    private void AddNamespaceNative(
            string owner, string namespaceName, string memberName, FunctionSignature signature, NativeFunction implementation) {
        RequireNamespaceOwner(owner, namespaceName, signature.Name);
        ClaimGlobal(owner, signature.Name, GrobValue.FromFunction(implementation));
        ClaimNamespace(owner, namespaceName);
        Members(_natives, namespaceName)[memberName] = new NativeEntry(signature, implementation, owner);
    }

    private static (string NamespaceName, string MemberName) SplitQualified(string name, string paramName) {
        Identifiers.RequireNamespaceQualified(name, paramName);
        int dot = name.IndexOf('.');
        return (name[..dot], name[(dot + 1)..]);
    }

    private static void RequireNotReserved(string namespaceName, string owner) {
        if (PrimitiveReceivers.IsReserved(namespaceName)) {
            throw new InvalidOperationException(
                $"'{namespaceName}' is a primitive type name and cannot be a namespace of plugin '{owner}'; primitive members register through the first-party primitive surface.");
        }
    }

    private void RequireNamespaceOwner(string owner, string namespaceName, string qualifiedName) {
        if (_namespaceOwners.TryGetValue(namespaceName, out string? existingOwner) && existingOwner != owner) {
            throw new InvalidOperationException(
                $"Namespace '{namespaceName}' belongs to plugin '{existingOwner}'; plugin '{owner}' cannot register '{qualifiedName}' in it.");
        }
    }

    private void ClaimGlobal(string owner, string name, GrobValue value) {
        if (_globalOwners.TryGetValue(name, out string? existingOwner)) {
            throw new InvalidOperationException(
                $"'{name}' is already registered by plugin '{existingOwner}'; plugin '{owner}' cannot register it again.");
        }
        _globalOwners[name] = owner;
        _globals[name] = value;
    }

    private void ClaimNamespace(string owner, string namespaceName) {
        if (_namespaceOwners.TryAdd(namespaceName, owner)) {
            _namespaceNames.Add(namespaceName);
        }
    }

    private static Dictionary<string, T> Members<T>(Dictionary<string, Dictionary<string, T>> namespaces, string namespaceName) {
        if (!namespaces.TryGetValue(namespaceName, out Dictionary<string, T>? members)) {
            members = new Dictionary<string, T>(StringComparer.Ordinal);
            namespaces[namespaceName] = members;
        }
        return members;
    }

    private static SignatureType.Concrete DeriveConstantType(GrobValue value) => value.Kind switch {
        GrobValueKind.Int => SignatureType.Of(GrobType.Int),
        GrobValueKind.Float => SignatureType.Of(GrobType.Float),
        GrobValueKind.String => SignatureType.Of(GrobType.String),
        GrobValueKind.Bool => SignatureType.Of(GrobType.Bool),
        GrobValueKind.Struct when !value.AsStruct().IsAnonymous => SignatureType.Named(GrobType.Struct, value.AsStruct().TypeName),
        _ => throw new ArgumentException(
            $"A constant must be an int, float, string, bool or named struct value; a {value.Kind} value is not.", "value"),
    };

    private sealed class PrimitiveTables {
        public Dictionary<string, PrimitivePropertyEntry> Properties { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, PrimitiveMethodEntry> Methods { get; } = new(StringComparer.Ordinal);

        public PrimitiveMemberSurface ToSurface(GrobType receiver) => new(
            receiver,
            new ReadOnlyDictionary<string, PrimitivePropertyEntry>(Properties),
            new ReadOnlyDictionary<string, PrimitiveMethodEntry>(Methods));
    }

    /// <summary>
    /// The registrar one plugin sees: the public <see cref="IPluginRegistrar"/> and, for a
    /// first-party plugin, the internal <see cref="IPrimitiveMemberRegistrar"/>, both
    /// writing into the owning builder under the plugin's name.
    /// </summary>
    private sealed class PluginRegistrar(NativeCatalogueBuilder builder, string owner) : IPluginRegistrar, IPrimitiveMemberRegistrar {
        public void RegisterNative(FunctionSignature signature, NativeFunction implementation) =>
            builder.RegisterNative(owner, signature, implementation);

        public void RegisterNative(string name, NativeFunction fn) =>
            builder.RegisterUntypedNative(owner, name, fn);

        public void RegisterConstant(string name, GrobValue value) =>
            builder.RegisterConstant(owner, name, value);

        public void RegisterNamedType(NamedTypeDefinition definition) =>
            builder.RegisterNamedType(owner, definition);

        public string RenderValue(GrobValue value) => builder._display.Display(value);

        public void RegisterPrimitiveProperty(GrobType receiver, PropertySignature signature, NativeFunction implementation) =>
            builder.RegisterPrimitiveProperty(owner, receiver, signature, implementation);

        public void RegisterPrimitiveMethod(GrobType receiver, FunctionSignature signature, NativeFunction implementation) =>
            builder.RegisterPrimitiveMethod(owner, receiver, signature, implementation);

        public void RegisterPrimitiveStatic(FunctionSignature signature, NativeFunction implementation) =>
            builder.RegisterPrimitiveStatic(owner, signature, implementation);

        public void RegisterBuiltin(FunctionSignature signature, NativeFunction implementation) =>
            builder.RegisterBuiltin(owner, signature, implementation);
    }
}
