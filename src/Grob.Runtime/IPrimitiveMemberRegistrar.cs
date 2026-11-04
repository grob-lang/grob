using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The registration surface for members of the primitive receivers <c>int</c>,
/// <c>float</c>, <c>string</c> and <c>bool</c>, and for bare top-level built-ins.
/// Internal by design (D-426 Decision 3): a public way to add a method to
/// <c>string</c> would be extension methods by the back door. <c>Grob.Stdlib</c> reaches
/// it through <c>InternalsVisibleTo</c>; a third-party plugin cannot. A first-party
/// plugin opts in by implementing <see cref="IPrimitiveMemberPlugin"/>.
/// </summary>
internal interface IPrimitiveMemberRegistrar {
    /// <summary>
    /// Registers a property on <paramref name="receiver"/>. The runtime global is
    /// <c>{receiver}.{name}</c> (<c>string.length</c>).
    /// </summary>
    void RegisterPrimitiveProperty(GrobType receiver, PropertySignature signature, NativeFunction implementation);

    /// <summary>
    /// Registers a method on <paramref name="receiver"/>. The signature's name is bare
    /// (<c>trim</c>); the runtime global is <c>{receiver}.{name}</c>.
    /// </summary>
    void RegisterPrimitiveMethod(GrobType receiver, FunctionSignature signature, NativeFunction implementation);

    /// <summary>
    /// Registers a type-static native whose namespace is a primitive type name
    /// (<c>int.min</c>, <c>float.clamp</c>). The signature's name is qualified; the
    /// public <see cref="IPluginRegistrar.RegisterNative(FunctionSignature, NativeFunction)"/>
    /// rejects these namespaces (gate report F8), so first-party code claims them here.
    /// </summary>
    void RegisterPrimitiveStatic(FunctionSignature signature, NativeFunction implementation);

    /// <summary>
    /// Registers a bare top-level built-in (<c>input</c>). The public surface rejects a
    /// bare name; only first-party code reaches this.
    /// </summary>
    void RegisterBuiltin(FunctionSignature signature, NativeFunction implementation);
}
