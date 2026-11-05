using Grob.Core;

using Xunit;

using static Grob.Runtime.Tests.TestPlugins;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for the internal <see cref="IPrimitiveMemberRegistrar"/> surface a first-party
/// <see cref="IPrimitiveMemberPlugin"/> registers through (D-426 Decision 3): instance
/// members on <c>int</c>, <c>float</c>, <c>string</c> and <c>bool</c>, type-statics in a
/// primitive-named namespace (<c>int.min</c>, gate report F8) and bare built-ins
/// (<c>input</c>). Registering a primitive member never makes the receiver a namespace,
/// so <c>string := 5</c> stays legal.
/// </summary>
public sealed class PrimitiveSurfaceTests {
    private static NativeCatalogue Build(Action<IPrimitiveMemberRegistrar> register, string owner = "stdlib") =>
        new NativeCatalogueBuilder().Add(new PrimitivePlugin(owner, register)).Build();

    // ----- Properties and methods -----

    [Fact]
    public void RegisterPrimitiveProperty_AppearsOnTheReceiverAndInGlobals() {
        NativeFunction length = Native("string.length", 1);
        NativeCatalogue catalogue = Build(r =>
            r.RegisterPrimitiveProperty(GrobType.String, new PropertySignature("length", Int), length));

        Assert.True(catalogue.TryGetPrimitiveMembers(GrobType.String, out PrimitiveMemberSurface? surface));
        Assert.Equal(GrobType.String, surface!.Receiver);
        PrimitivePropertyEntry entry = surface.Properties["length"];
        Assert.Equal("length", entry.Signature.Name);
        Assert.Equal("string.length", entry.QualifiedNativeName);
        Assert.Same(length, entry.Implementation);
        Assert.Same(length, catalogue.Globals["string.length"].AsFunction());
        Assert.Empty(surface.Methods);
    }

    [Fact]
    public void RegisterPrimitiveMethod_AppearsOnTheReceiverAndInGlobals() {
        NativeFunction trim = Native("string.trim", 1);
        NativeCatalogue catalogue = Build(r => r.RegisterPrimitiveMethod(GrobType.String, Signature("trim"), trim));

        Assert.True(catalogue.TryGetPrimitiveMembers(GrobType.String, out PrimitiveMemberSurface? surface));
        PrimitiveMethodEntry entry = surface!.Methods["trim"];
        Assert.Equal("trim", entry.Signature.Name);
        Assert.Equal("string.trim", entry.QualifiedNativeName);
        Assert.Same(trim, entry.Implementation);
        Assert.Same(trim, catalogue.Globals["string.trim"].AsFunction());
    }

    [Theory]
    [InlineData(GrobType.Int, "int")]
    [InlineData(GrobType.Float, "float")]
    [InlineData(GrobType.Bool, "bool")]
    public void RegisterPrimitiveMethod_EachReceiver_UsesItsPrefix(GrobType receiver, string prefix) {
        NativeCatalogue catalogue = Build(r => r.RegisterPrimitiveMethod(receiver, Signature("toString"), Native("x")));

        Assert.True(catalogue.TryGetPrimitiveMembers(receiver, out PrimitiveMemberSurface? surface));
        Assert.Equal(prefix + ".toString", surface!.Methods["toString"].QualifiedNativeName);
        Assert.True(catalogue.Globals.ContainsKey(prefix + ".toString"));
    }

    [Fact]
    public void RegisterPrimitiveMember_DoesNotMakeTheReceiverANamespace() {
        NativeCatalogue catalogue = Build(r => {
            r.RegisterPrimitiveProperty(GrobType.String, new PropertySignature("length", Int), Native("x"));
            r.RegisterPrimitiveMethod(GrobType.Bool, Signature("toString"), Native("y"));
        });

        Assert.Empty(catalogue.NamespaceNames);
        Assert.False(catalogue.IsNamespace("string"));
        Assert.False(catalogue.IsNamespace("bool"));
    }

    [Fact]
    public void RegisterPrimitiveMembers_RunsAfterRegister() {
        var order = new List<string>();
        var plugin = new PrimitivePlugin("stdlib", r => order.Add("primitives"), r => order.Add("register"));

        new NativeCatalogueBuilder().Add(plugin).Build();

        Assert.Equal(["register", "primitives"], order);
    }

    [Theory]
    [InlineData(GrobType.Struct)]
    [InlineData(GrobType.Array)]
    [InlineData(GrobType.NullableInt)]
    [InlineData(GrobType.Nil)]
    public void RegisterPrimitiveMember_NonPrimitiveReceiver_Throws(GrobType receiver) {
        var property = Assert.Throws<ArgumentException>(() =>
            Build(r => r.RegisterPrimitiveProperty(receiver, new PropertySignature("p", Int), Native("x"))));
        var method = Assert.Throws<ArgumentException>(() =>
            Build(r => r.RegisterPrimitiveMethod(receiver, Signature("m"), Native("x"))));

        Assert.Equal("receiver", property.ParamName);
        Assert.Equal("receiver", method.ParamName);
        Assert.Contains(receiver.ToString(), method.Message);
    }

    [Fact]
    public void RegisterPrimitiveMethod_QualifiedName_Throws() {
        var ex = Assert.Throws<ArgumentException>(() =>
            Build(r => r.RegisterPrimitiveMethod(GrobType.String, Signature("string.trim"), Native("x"))));

        Assert.Equal("signature", ex.ParamName);
        Assert.Contains("bare", ex.Message);
    }

    [Fact]
    public void RegisterPrimitiveMember_NullArguments_Throw() {
        Assert.Throws<ArgumentNullException>(() =>
            Build(r => r.RegisterPrimitiveProperty(GrobType.String, null!, Native("x"))));
        Assert.Throws<ArgumentNullException>(() =>
            Build(r => r.RegisterPrimitiveProperty(GrobType.String, new PropertySignature("p", Int), null!)));
        Assert.Throws<ArgumentNullException>(() =>
            Build(r => r.RegisterPrimitiveMethod(GrobType.String, null!, Native("x"))));
        Assert.Throws<ArgumentNullException>(() =>
            Build(r => r.RegisterPrimitiveMethod(GrobType.String, Signature("m"), null!)));
    }

    [Fact]
    public void RegisterPrimitiveMember_SameNameTwice_NamesBothOwners() {
        var builder = new NativeCatalogueBuilder()
            .Add(new PrimitivePlugin("first", r => r.RegisterPrimitiveMethod(GrobType.String, Signature("trim"), Native("x"))));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Add(new PrimitivePlugin("second", r => r.RegisterPrimitiveProperty(GrobType.String, new PropertySignature("trim", Int), Native("y")))));

        Assert.Contains("'string.trim'", ex.Message);
        Assert.Contains("'first'", ex.Message);
        Assert.Contains("'second'", ex.Message);
    }

    [Theory]
    [InlineData("property")]
    [InlineData("method")]
    [InlineData("static")]
    [InlineData("builtin")]
    public void RegisterPrimitive_AfterBuildThroughARetainedRegistrar_Throws(string call) {
        IPrimitiveMemberRegistrar? retained = null;
        var builder = new NativeCatalogueBuilder().Add(new PrimitivePlugin("keeper", r => retained = r));
        NativeCatalogue catalogue = builder.Build();
        Action late = call switch {
            "property" => () => retained!.RegisterPrimitiveProperty(GrobType.String, new PropertySignature("late", Int), Native("x")),
            "method" => () => retained!.RegisterPrimitiveMethod(GrobType.String, Signature("late"), Native("x")),
            "static" => () => retained!.RegisterPrimitiveStatic(Signature("int.late"), Native("x")),
            _ => () => retained!.RegisterBuiltin(Signature("late"), Native("x")),
        };

        var ex = Assert.Throws<InvalidOperationException>(late);

        Assert.Contains("built", ex.Message);
        Assert.Empty(catalogue.Globals);
    }

    // ----- Type-statics in a primitive-named namespace -----

    [Fact]
    public void RegisterPrimitiveStatic_ClaimsThePrimitiveNamespace() {
        NativeFunction min = Native("int.min", 2);
        NativeCatalogue catalogue = Build(r => r.RegisterPrimitiveStatic(
            Signature("int.min", new SignatureParameter("a", Int), new SignatureParameter("b", Int)), min));

        Assert.True(catalogue.IsNamespace("int"));
        Assert.True(catalogue.TryGetNative("int", "min", out NativeEntry? entry));
        Assert.Same(min, entry!.Implementation);
        Assert.Equal("stdlib", entry.Owner);
        Assert.Same(min, catalogue.Globals["int.min"].AsFunction());
        Assert.False(catalogue.TryGetPrimitiveMembers(GrobType.Int, out _));
    }

    [Fact]
    public void RegisterPrimitiveStatic_NonPrimitiveNamespace_Throws() {
        var ex = Assert.Throws<ArgumentException>(() =>
            Build(r => r.RegisterPrimitiveStatic(Signature("math.min"), Native("x"))));

        Assert.Equal("signature", ex.ParamName);
        Assert.Contains("'math'", ex.Message);
    }

    [Fact]
    public void RegisterPrimitiveStatic_BareName_Throws() {
        var ex = Assert.Throws<ArgumentException>(() =>
            Build(r => r.RegisterPrimitiveStatic(Signature("min"), Native("x"))));

        Assert.Equal("signature", ex.ParamName);
    }

    [Fact]
    public void RegisterPrimitiveStatic_IntoANamespaceAnotherPluginClaimed_Throws() {
        var builder = new NativeCatalogueBuilder()
            .Add(new PrimitivePlugin("first", r => r.RegisterPrimitiveStatic(Signature("int.min"), Native("x"))));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Add(new PrimitivePlugin("second", r => r.RegisterPrimitiveStatic(Signature("int.max"), Native("y")))));

        Assert.Contains("'int'", ex.Message);
        Assert.Contains("'first'", ex.Message);
        Assert.Contains("'second'", ex.Message);
    }

    [Fact]
    public void RegisterPrimitiveStatic_NullArguments_Throw() {
        Assert.Throws<ArgumentNullException>(() => Build(r => r.RegisterPrimitiveStatic(null!, Native("x"))));
        Assert.Throws<ArgumentNullException>(() => Build(r => r.RegisterPrimitiveStatic(Signature("int.min"), null!)));
    }

    // ----- Bare built-ins -----

    [Fact]
    public void RegisterBuiltin_AppearsInBuiltinsAndGlobals() {
        NativeFunction input = Native("input", 1);
        NativeCatalogue catalogue = Build(r => r.RegisterBuiltin(
            new FunctionSignature("input", [new SignatureParameter("prompt", TestPlugins.String, defaultValue: GrobValue.FromString(""))], TestPlugins.String), input));

        Assert.True(catalogue.TryGetBuiltin("input", out NativeEntry? entry));
        Assert.Equal("input", entry!.Signature.Name);
        Assert.Same(input, entry.Implementation);
        Assert.Equal("stdlib", entry.Owner);
        Assert.Same(input, catalogue.Globals["input"].AsFunction());
        Assert.Empty(catalogue.NamespaceNames);
    }

    [Fact]
    public void RegisterBuiltin_QualifiedName_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => Build(r => r.RegisterBuiltin(Signature("io.input"), Native("x"))));

        Assert.Equal("signature", ex.ParamName);
        Assert.Contains("bare", ex.Message);
    }

    [Fact]
    public void RegisterBuiltin_Twice_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(r => {
            r.RegisterBuiltin(Signature("input"), Native("x"));
            r.RegisterBuiltin(Signature("input"), Native("y"));
        }));

        Assert.Contains("'input'", ex.Message);
    }

    [Fact]
    public void RegisterBuiltin_NullArguments_Throw() {
        Assert.Throws<ArgumentNullException>(() => Build(r => r.RegisterBuiltin(null!, Native("x"))));
        Assert.Throws<ArgumentNullException>(() => Build(r => r.RegisterBuiltin(Signature("input"), null!)));
    }
}
