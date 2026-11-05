using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The registration surface an <see cref="IGrobPlugin"/> registers natives, namespace
/// constants and named types against (Sprint 8 Increment A; D-426). Declared in
/// <c>Grob.Runtime</c>, the published NuGet surface, so a plugin author never sees a host
/// type. The one implementation is the registrar <see cref="NativeCatalogueBuilder"/>
/// hands each plugin: every registration lands in one <see cref="NativeCatalogue"/>, which
/// the type checker reads and the VM is constructed over (D-426 Decision 2). There is no
/// second registration path.
/// </summary>
public interface IPluginRegistrar {
    /// <summary>
    /// Registers <paramref name="implementation"/> as the namespace native
    /// <paramref name="signature"/> describes (D-426). The signature's name must be
    /// qualified (<c>"math.sqrt"</c>); a bare name is rejected, and a namespace that is a
    /// primitive type name (<c>int</c>, <c>float</c>, <c>string</c>, <c>bool</c>) is
    /// reserved for first-party code. The type checker reads the signature from the
    /// catalogue this registration builds; the VM binds the implementation from it.
    /// </summary>
    void RegisterNative(FunctionSignature signature, NativeFunction implementation);

    /// <summary>
    /// Registers <paramref name="fn"/> as a global callable under <paramref name="name"/>
    /// (a qualified name such as <c>"math.sqrt"</c> for a namespace member, or a bare name
    /// for a top-level built-in) with no signature. Transitional: it survives only until
    /// every first-party registration carries a signature
    /// (<see cref="RegisterNative(FunctionSignature, NativeFunction)"/>), when it is
    /// deleted (D-426, change group 7).
    /// </summary>
    void RegisterNative(string name, NativeFunction fn);

    /// <summary>
    /// Registers a named type this plugin owns: its properties, its methods and the
    /// renderer <c>print()</c> uses for it (D-426 Decision 3). The checker resolves the
    /// type's members from the definition and the VM dispatches through its bindings.
    /// Registering a type another plugin already registered is a host error.
    /// </summary>
    void RegisterNamedType(NamedTypeDefinition definition);

    /// <summary>
    /// Registers <paramref name="value"/> as a global constant under
    /// <paramref name="name"/> — the runtime counterpart of a namespace constant such as
    /// <c>math.pi</c>, which has no callable behaviour to dispatch.
    /// </summary>
    void RegisterConstant(string name, GrobValue value);

    /// <summary>
    /// Renders <paramref name="value"/> through the catalogue's <c>ValueDisplay</c>
    /// (D-336) — the same top-level rendering <c>print()</c> uses, including the renderer
    /// of any named type any plugin registers (regardless of registration order, since
    /// this always consults the renderer table's current state, not a snapshot taken at
    /// <c>Register</c> time). The seam <c>formatAs</c> (Sprint 8 Increment E,
    /// <c>Grob.Stdlib</c>) uses to render cell values — a bare <c>ValueDisplay</c> of its
    /// own would miss a plugin type's renderer (e.g. <c>guid</c>). A renderer arrives only
    /// with its named type (<see cref="RegisterNamedType"/>): a struct-typed value a
    /// plugin returns is by construction a named type, so there is no renderer to
    /// register without one (D-426; the former <c>RegisterToString</c> is retired).
    /// </summary>
    string RenderValue(GrobValue value);
}
