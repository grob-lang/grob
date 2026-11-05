using Grob.Core;
using Grob.Runtime;

namespace Grob.Vm.Tests;

/// <summary>
/// Catalogues for VM tests that need one registration without a stdlib plugin
/// (<c>Grob.Vm.Tests</c> references <c>Grob.Vm</c> alone): a renderer for a struct type
/// name, so a test can prove <see cref="OpCode.Print"/> renders through the catalogue's
/// renderer table rather than through the struct's fields (D-336, D-426).
/// </summary>
internal static class TestCatalogues {
    /// <summary>A catalogue holding one named type, <paramref name="typeName"/>, with no members and the given renderer.</summary>
    internal static NativeCatalogue RendererFor(string typeName, Func<GrobValue, string> render) =>
        new NativeCatalogueBuilder()
            .Add(new RendererPlugin(typeName, render))
            .Build();

    private sealed class RendererPlugin(string typeName, Func<GrobValue, string> render) : IGrobPlugin {
        public string Name => typeName;

        public void Register(IPluginRegistrar registrar) =>
            registrar.RegisterNamedType(new NamedTypeDefinition(typeName, [], [], render));
    }
}
