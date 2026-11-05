using System.Reflection;
using System.Runtime.CompilerServices;

using Grob.Core;

using Xunit;

using static Grob.Runtime.Tests.TestPlugins;

namespace Grob.Runtime.Tests;

/// <summary>
/// Pins the mechanism that keeps primitive receivers closed to third-party plugins
/// (D-426 Decision 3): the primitive surface is <c>internal</c>, and
/// <c>Grob.Runtime</c> grants internal visibility to exactly <c>Grob.Runtime.Tests</c>,
/// <c>Grob.Vm</c> and <c>Grob.Stdlib</c>. The behavioural test shows what a plugin with
/// only the public surface can do about <c>string</c>: nothing.
/// </summary>
public sealed class PrimitiveSurfaceClosureTests {
    private static readonly Assembly Runtime = typeof(NativeCatalogue).Assembly;

    [Theory]
    [InlineData("Grob.Runtime.IPrimitiveMemberRegistrar")]
    [InlineData("Grob.Runtime.IPrimitiveMemberPlugin")]
    public void PrimitiveSurfaceType_IsNotPublic(string typeName) {
        Type? type = Runtime.GetType(typeName, throwOnError: true);

        Assert.True(type!.IsNotPublic, $"{typeName} must stay internal.");
    }

    [Fact]
    public void InternalsVisibleTo_NamesExactlyTheThreeFirstPartyAssemblies() {
        string[] grantees = [.. Runtime
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .Order(StringComparer.Ordinal)];

        Assert.Equal(["Grob.Runtime.Tests", "Grob.Stdlib", "Grob.Vm"], grantees);
    }

    [Fact]
    public void PublicRegistrar_HasNoPrimitiveMemberMethod() {
        IEnumerable<string> names = typeof(IPluginRegistrar).GetMethods().Select(m => m.Name);

        Assert.DoesNotContain(names, n => n.Contains("Primitive", StringComparison.Ordinal) || n == "RegisterBuiltin");
    }

    [Fact]
    public void PublicOnlyPlugin_CannotGiveStringAMember() {
        var builder = new NativeCatalogueBuilder();
        var shout = new Plugin("third", r => r.RegisterNative(Signature("string.shout"), Native("string.shout", 1)));

        Assert.Throws<InvalidOperationException>(() => builder.Add(shout));
        NativeCatalogue catalogue = builder.Build();

        Assert.False(catalogue.TryGetPrimitiveMembers(GrobType.String, out _));
        Assert.False(catalogue.Globals.ContainsKey("string.shout"));
        Assert.False(catalogue.IsNamespace("string"));
    }
}
