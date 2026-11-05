using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Builds the catalogue a plugin test runs its VM over (D-426 Decision 2): the plugin
/// registers into a <see cref="NativeCatalogueBuilder"/> and the VM is constructed over
/// the result, which is the one path a real host takes too.
/// </summary>
internal static class TestCatalogue {
    /// <summary>One catalogue over <paramref name="plugins"/>, in the given order.</summary>
    internal static NativeCatalogue Catalogue(params IGrobPlugin[] plugins) {
        var builder = new NativeCatalogueBuilder();
        foreach (IGrobPlugin plugin in plugins) builder.Add(plugin);
        return builder.Build();
    }

    /// <summary>
    /// A plugin that registers only a renderer for <paramref name="typeName"/>: what a
    /// test needs to prove a struct renders through its named type's renderer and not
    /// through its fields.
    /// </summary>
    internal static IGrobPlugin RendererFor(string typeName, Func<GrobValue, string> render) =>
        new DelegatePlugin(typeName, r => r.RegisterNamedType(new NamedTypeDefinition(typeName, [], [], render)));

    private sealed class DelegatePlugin(string name, Action<IPluginRegistrar> register) : IGrobPlugin {
        public string Name => name;

        public void Register(IPluginRegistrar registrar) => register(registrar);
    }
}
