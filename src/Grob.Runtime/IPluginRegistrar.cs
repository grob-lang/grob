using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The registration surface an <see cref="IGrobPlugin"/> registers natives and namespace
/// constants against (Sprint 8 Increment A). Declared in <c>Grob.Runtime</c> — not the
/// concrete VM type — because the DAG forbids <c>Grob.Runtime</c> from referencing
/// <c>Grob.Vm</c> (<c>Grob.Vm</c> already references <c>Grob.Runtime</c>; the reverse edge
/// would cycle). <c>VirtualMachine</c> implements this interface in <c>Grob.Vm</c>, so a
/// plugin author writing against <c>Grob.Runtime</c> (the published NuGet surface) never
/// needs to see the VM's concrete type.
/// </summary>
public interface IPluginRegistrar {
    /// <summary>
    /// Registers <paramref name="fn"/> as a global callable under <paramref name="name"/>
    /// (a qualified name such as <c>"math.sqrt"</c> for a namespace member, or a bare name
    /// for a top-level built-in).
    /// </summary>
    void RegisterNative(string name, NativeFunction fn);

    /// <summary>
    /// Registers <paramref name="value"/> as a global constant under
    /// <paramref name="name"/> — the runtime counterpart of a namespace constant such as
    /// <c>math.pi</c>, which has no callable behaviour to dispatch.
    /// </summary>
    void RegisterConstant(string name, GrobValue value);

    /// <summary>
    /// Registers <paramref name="toString"/> as the <c>ValueDisplay</c> (D-336) renderer
    /// for every <c>Struct</c>-kind value whose <c>GrobStruct.TypeName</c> is
    /// <paramref name="typeName"/> — the seam a plugin-owned type (<c>guid</c>, Sprint 8
    /// Increment D) uses to make <c>print()</c> and string interpolation render its
    /// canonical form instead of falling through to structural field rendering.
    /// </summary>
    void RegisterToString(string typeName, Func<GrobValue, string> toString);
}
