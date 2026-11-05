using Grob.Core;

using Xunit;

using static Grob.Runtime.Tests.TestPlugins;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for <see cref="NativeCatalogue"/> as a read surface: the empty catalogue, lookup
/// misses, and the <see cref="IValueToStringRegistry"/> seam a <see cref="ValueDisplay"/>
/// is built over so <c>print()</c> and <c>formatAs</c> render through one renderer table
/// (D-336, gate report §1.3).
/// </summary>
public sealed class NativeCatalogueTests {
    [Fact]
    public void Empty_HasNoNamespacesTypesOrGlobals() {
        NativeCatalogue empty = NativeCatalogue.Empty;

        Assert.Empty(empty.NamespaceNames);
        Assert.Empty(empty.NamedTypeNames);
        Assert.Empty(empty.Globals);
        Assert.False(empty.IsNamespace("math"));
        Assert.False(empty.TryGetNative("math", "sqrt", out _));
        Assert.False(empty.TryGetConstant("math", "pi", out _));
        Assert.False(empty.TryGetPrimitiveMembers(GrobType.String, out _));
        Assert.False(empty.TryGetNamedType("date", out _));
        Assert.False(empty.TryGetBuiltin("input", out _));
    }

    [Fact]
    public void Empty_IsOneSharedInstance() =>
        Assert.Same(NativeCatalogue.Empty, NativeCatalogue.Empty);

    [Fact]
    public void Empty_RendersStructurally() =>
        Assert.Equal("thing { value: 1 }", NativeCatalogue.Empty.RenderValue(StructOf("thing", 1)));

    [Fact]
    public void TryGetNative_UnknownMemberOfKnownNamespace_IsFalse() {
        NativeCatalogue catalogue = new NativeCatalogueBuilder()
            .Add(new Plugin("math", r => r.RegisterNative(Signature("math.sqrt"), Native("math.sqrt"))))
            .Build();

        Assert.False(catalogue.TryGetNative("math", "cbrt", out NativeEntry? entry));
        Assert.Null(entry);
        Assert.False(catalogue.TryGetConstant("math", "sqrt", out _));
    }

    [Fact]
    public void TryGetNative_NullArguments_Throw() {
        NativeCatalogue catalogue = NativeCatalogue.Empty;

        Assert.Throws<ArgumentNullException>(() => catalogue.TryGetNative(null!, "x", out _));
        Assert.Throws<ArgumentNullException>(() => catalogue.TryGetNative("x", null!, out _));
        Assert.Throws<ArgumentNullException>(() => catalogue.TryGetConstant(null!, "x", out _));
        Assert.Throws<ArgumentNullException>(() => catalogue.TryGetConstant("x", null!, out _));
        Assert.Throws<ArgumentNullException>(() => catalogue.TryGetNamedType(null!, out _));
        Assert.Throws<ArgumentNullException>(() => catalogue.TryGetBuiltin(null!, out _));
        Assert.Throws<ArgumentNullException>(() => catalogue.IsNamespace(null!));
    }

    [Fact]
    public void NamespaceNames_FollowRegistrationOrder() {
        NativeCatalogue catalogue = new NativeCatalogueBuilder()
            .Add(new Plugin("z", r => r.RegisterNative(Signature("zeta.one"), Native("zeta.one"))))
            .Add(new Plugin("a", r => r.RegisterConstant("alpha.one", GrobValue.FromInt(1))))
            .Build();

        Assert.Equal(["zeta", "alpha"], catalogue.NamespaceNames);
    }

    [Fact]
    public void ValueDisplay_OverTheCatalogue_UsesItsRenderers() {
        NativeCatalogue catalogue = new NativeCatalogueBuilder()
            .Add(new Plugin("guid", r => r.RegisterToString("guid", v => "rendered")))
            .Build();
        var display = new ValueDisplay(catalogue);

        Assert.Equal("rendered", display.Display(StructOf("guid", 1)));
        Assert.Equal("[rendered]", display.Display(GrobValue.FromArray(new GrobArray([StructOf("guid", 1)]))));
    }

    [Fact]
    public void TryToString_NonStructValue_Misses() {
        IValueToStringRegistry registry = new NativeCatalogueBuilder()
            .Add(new Plugin("guid", r => r.RegisterToString("guid", v => "rendered")))
            .Build();

        Assert.False(registry.TryToString(GrobValue.FromInt(1), out string? rendered));
        Assert.Null(rendered);
    }
}
