using Grob.Core;

using Xunit;

using static Grob.Runtime.Tests.TestPlugins;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for <see cref="NativeCatalogueBuilder"/> through the public
/// <see cref="IPluginRegistrar"/> surface (D-426 Decision 2): one registration pass
/// produces one catalogue, duplicate registration is a host error naming both owners,
/// a namespace belongs to the plugin that first registered into it, and a primitive type
/// name cannot be claimed as a namespace from the public surface (D-426 Decision 3,
/// gate report F8). The untyped <c>RegisterNative</c> overload survives until group 7
/// and writes globals only.
/// </summary>
public sealed class NativeCatalogueBuilderTests {
    private static NativeCatalogue Build(params IGrobPlugin[] plugins) {
        var builder = new NativeCatalogueBuilder();
        foreach (IGrobPlugin plugin in plugins) builder.Add(plugin);
        return builder.Build();
    }

    // ----- Typed natives -----

    [Fact]
    public void RegisterNative_Typed_AppearsInNamespaceAndGlobals() {
        NativeFunction sqrt = Native("math.sqrt", 1);
        NativeCatalogue catalogue = Build(new Plugin("math", r =>
            r.RegisterNative(Signature("math.sqrt", new SignatureParameter("n", Float)), sqrt)));

        Assert.True(catalogue.IsNamespace("math"));
        Assert.Equal(["math"], catalogue.NamespaceNames);
        Assert.True(catalogue.TryGetNative("math", "sqrt", out NativeEntry? entry));
        Assert.Equal("math.sqrt", entry!.Signature.Name);
        Assert.Same(sqrt, entry.Implementation);
        Assert.Equal("math", entry.Owner);
        Assert.Same(sqrt, catalogue.Globals["math.sqrt"].AsFunction());
    }

    [Fact]
    public void RegisterNative_Typed_NullSignature_Throws() {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            Build(new Plugin("p", r => r.RegisterNative((FunctionSignature)null!, Native("x")))));

        Assert.Equal("signature", ex.ParamName);
    }

    [Fact]
    public void RegisterNative_Typed_NullImplementation_Throws() {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            Build(new Plugin("p", r => r.RegisterNative(Signature("p.x"), null!))));

        Assert.Equal("implementation", ex.ParamName);
    }

    [Fact]
    public void RegisterNative_Typed_BareName_Throws() {
        var ex = Assert.Throws<ArgumentException>(() =>
            Build(new Plugin("io", r => r.RegisterNative(Signature("input"), Native("input")))));

        Assert.Equal("signature", ex.ParamName);
        Assert.Contains("'input'", ex.Message);
        Assert.Contains("qualified", ex.Message);
    }

    [Fact]
    public void RegisterNative_Typed_FlatKeyMember_IsKeyedAfterTheFirstDot() {
        NativeCatalogue catalogue = Build(new Plugin("guid", r =>
            r.RegisterNative(Signature("guid.namespaces.dns"), Native("guid.namespaces.dns"))));

        Assert.True(catalogue.TryGetNative("guid", "namespaces.dns", out _));
        Assert.False(catalogue.IsNamespace("guid.namespaces"));
    }

    // ----- Untyped natives (transitional) -----

    [Fact]
    public void RegisterNative_Untyped_AppearsInGlobalsOnly() {
        NativeFunction input = Native("input", 1);
        NativeCatalogue catalogue = Build(new Plugin("io", r => r.RegisterNative("input", input)));

        Assert.Same(input, catalogue.Globals["input"].AsFunction());
        Assert.Empty(catalogue.NamespaceNames);
        Assert.False(catalogue.TryGetBuiltin("input", out _));
    }

    [Fact]
    public void RegisterNative_Untyped_DoesNotClaimANamespace() {
        NativeCatalogue catalogue = Build(new Plugin("statics", r => r.RegisterNative("int.min", Native("int.min", 2))));

        Assert.False(catalogue.IsNamespace("int"));
        Assert.True(catalogue.Globals.ContainsKey("int.min"));
    }

    [Fact]
    public void RegisterNative_Untyped_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Build(new Plugin("p", r => r.RegisterNative((string)null!, Native("x")))));

    [Fact]
    public void RegisterNative_Untyped_NullImplementation_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Build(new Plugin("p", r => r.RegisterNative("x", null!))));

    // ----- Constants -----

    [Fact]
    public void RegisterConstant_Int_DerivesIntType() {
        NativeCatalogue catalogue = Build(new Plugin("ns", r => r.RegisterConstant("ns.answer", GrobValue.FromInt(42))));

        Assert.True(catalogue.TryGetConstant("ns", "answer", out ConstantEntry? entry));
        Assert.Equal("ns.answer", entry!.Name);
        Assert.Equal(Int, entry.Type);
        Assert.Equal(42, entry.Value.AsInt());
        Assert.Equal("ns", entry.Owner);
        Assert.Equal(42, catalogue.Globals["ns.answer"].AsInt());
        Assert.True(catalogue.IsNamespace("ns"));
    }

    [Theory]
    [InlineData(GrobValueKind.Float, GrobType.Float)]
    [InlineData(GrobValueKind.String, GrobType.String)]
    [InlineData(GrobValueKind.Bool, GrobType.Bool)]
    public void RegisterConstant_Scalar_DerivesItsType(GrobValueKind kind, GrobType expected) {
        GrobValue value = kind switch {
            GrobValueKind.Float => GrobValue.FromFloat(3.14),
            GrobValueKind.String => GrobValue.FromString("/"),
            _ => GrobValue.FromBool(true),
        };
        NativeCatalogue catalogue = Build(new Plugin("ns", r => r.RegisterConstant("ns.c", value)));

        Assert.True(catalogue.TryGetConstant("ns", "c", out ConstantEntry? entry));
        Assert.Equal(SignatureType.Of(expected), entry!.Type);
    }

    [Fact]
    public void RegisterConstant_Struct_DerivesTheNamedType() {
        NativeCatalogue catalogue = Build(new Plugin("guid", r => r.RegisterConstant("guid.empty", StructOf("guid", 0))));

        Assert.True(catalogue.TryGetConstant("guid", "empty", out ConstantEntry? entry));
        Assert.Equal(SignatureType.Named(GrobType.Struct, "guid"), entry!.Type);
    }

    [Fact]
    public void RegisterConstant_NonScalarValue_Throws() {
        var ex = Assert.Throws<ArgumentException>(() =>
            Build(new Plugin("ns", r => r.RegisterConstant("ns.list", GrobValue.FromArray(new GrobArray())))));

        Assert.Equal("value", ex.ParamName);
        Assert.Contains("Array", ex.Message);
    }

    [Fact]
    public void RegisterConstant_Nil_Throws() {
        var ex = Assert.Throws<ArgumentException>(() =>
            Build(new Plugin("ns", r => r.RegisterConstant("ns.nothing", GrobValue.Nil))));

        Assert.Equal("value", ex.ParamName);
    }

    [Fact]
    public void RegisterConstant_BareName_Throws() {
        var ex = Assert.Throws<ArgumentException>(() =>
            Build(new Plugin("ns", r => r.RegisterConstant("pi", GrobValue.FromFloat(3.14)))));

        Assert.Equal("name", ex.ParamName);
        Assert.Contains("qualified", ex.Message);
    }

    [Fact]
    public void RegisterConstant_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Build(new Plugin("ns", r => r.RegisterConstant(null!, GrobValue.FromInt(1)))));

    // ----- Duplicate registration is a host error -----

    [Fact]
    public void RegisterNative_SameNameTwiceInOnePlugin_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(new Plugin("math", r => {
            r.RegisterNative(Signature("math.sqrt"), Native("math.sqrt"));
            r.RegisterNative(Signature("math.sqrt"), Native("math.sqrt"));
        })));

        Assert.Contains("'math.sqrt'", ex.Message);
        Assert.Contains("'math'", ex.Message);
    }

    [Fact]
    public void RegisterNative_TypedThenUntypedSameName_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(new Plugin("math", r => {
            r.RegisterNative(Signature("math.sqrt"), Native("math.sqrt"));
            r.RegisterNative("math.sqrt", Native("math.sqrt"));
        })));

        Assert.Contains("'math.sqrt'", ex.Message);
    }

    [Fact]
    public void RegisterConstant_OverAnExistingNative_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(new Plugin("math", r => {
            r.RegisterNative(Signature("math.pi"), Native("math.pi"));
            r.RegisterConstant("math.pi", GrobValue.FromFloat(3.14));
        })));

        Assert.Contains("'math.pi'", ex.Message);
    }

    [Fact]
    public void RegisterNative_Untyped_SameNameFromTwoPlugins_NamesBothOwners() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            new Plugin("first", r => r.RegisterNative("shared", Native("shared"))),
            new Plugin("second", r => r.RegisterNative("shared", Native("shared")))));

        Assert.Contains("'shared'", ex.Message);
        Assert.Contains("'first'", ex.Message);
        Assert.Contains("'second'", ex.Message);
    }

    // ----- Namespace ownership -----

    [Fact]
    public void RegisterNative_IntoAnotherPluginsNamespace_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            new Plugin("math", r => r.RegisterNative(Signature("math.sqrt"), Native("math.sqrt"))),
            new Plugin("intruder", r => r.RegisterNative(Signature("math.cbrt"), Native("math.cbrt")))));

        Assert.Contains("'math'", ex.Message);
        Assert.Contains("'intruder'", ex.Message);
        Assert.Contains("'math.cbrt'", ex.Message);
    }

    [Fact]
    public void RegisterConstant_IntoAnotherPluginsNamespace_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            new Plugin("math", r => r.RegisterNative(Signature("math.sqrt"), Native("math.sqrt"))),
            new Plugin("intruder", r => r.RegisterConstant("math.tau", GrobValue.FromFloat(6.28)))));

        Assert.Contains("'math'", ex.Message);
        Assert.Contains("'intruder'", ex.Message);
    }

    [Fact]
    public void RegisterNative_OnePluginSeveralNamespaces_IsPermitted() {
        NativeCatalogue catalogue = Build(new Plugin("numeric", r => {
            r.RegisterNative(Signature("a.one"), Native("a.one"));
            r.RegisterNative(Signature("b.one"), Native("b.one"));
        }));

        Assert.Equal(["a", "b"], catalogue.NamespaceNames);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("float")]
    [InlineData("string")]
    [InlineData("bool")]
    public void RegisterNative_Typed_PrimitiveNamespace_Throws(string primitive) {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Build(new Plugin("third", r => r.RegisterNative(Signature(primitive + ".shout"), Native("x")))));

        Assert.Contains($"'{primitive}'", ex.Message);
        Assert.Contains("'third'", ex.Message);
        Assert.Contains("primitive", ex.Message);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("string")]
    public void RegisterConstant_PrimitiveNamespace_Throws(string primitive) {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            Build(new Plugin("third", r => r.RegisterConstant(primitive + ".answer", GrobValue.FromInt(42)))));

        Assert.Contains($"'{primitive}'", ex.Message);
        Assert.Contains("primitive", ex.Message);
    }

    [Fact]
    public void RegisterNative_Typed_PrimitiveNamespaceRejected_LeavesNoTrace() {
        var builder = new NativeCatalogueBuilder();
        var plugin = new Plugin("third", r => r.RegisterNative(Signature("string.shout"), Native("x")));

        Assert.Throws<InvalidOperationException>(() => builder.Add(plugin));
        NativeCatalogue catalogue = builder.Build();

        Assert.False(catalogue.Globals.ContainsKey("string.shout"));
        Assert.False(catalogue.IsNamespace("string"));
        Assert.False(catalogue.TryGetPrimitiveMembers(GrobType.String, out _));
    }

    // ----- Named types -----

    [Fact]
    public void RegisterNamedType_AppearsInNamedTypesAndRenders() {
        NamedTypeDefinition definition = NamedType("date");
        NativeCatalogue catalogue = Build(new Plugin("date", r => r.RegisterNamedType(definition)));

        Assert.Equal(["date"], catalogue.NamedTypeNames);
        Assert.True(catalogue.TryGetNamedType("date", out NamedTypeSurface? surface));
        Assert.Same(definition, surface!.Definition);
        Assert.Equal("date", surface.Owner);
        Assert.Same(definition.Properties[0], surface.Properties["value"]);
        Assert.Same(definition.Methods[0], surface.Methods["next"]);
        Assert.Equal("date:7", catalogue.RenderValue(StructOf("date", 7)));
    }

    [Fact]
    public void RegisterNamedType_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => Build(new Plugin("date", r => r.RegisterNamedType(null!))));

    [Fact]
    public void RegisterNamedType_Twice_NamesBothOwners() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            new Plugin("first", r => r.RegisterNamedType(NamedType("date"))),
            new Plugin("second", r => r.RegisterNamedType(NamedType("date")))));

        Assert.Contains("'date'", ex.Message);
        Assert.Contains("'first'", ex.Message);
        Assert.Contains("'second'", ex.Message);
    }

    [Fact]
    public void RegisterNamedType_AfterRegisterToStringForTheSameName_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(new Plugin("date", r => {
            r.RegisterToString("date", v => "old");
            r.RegisterNamedType(NamedType("date"));
        })));

        Assert.Contains("'date'", ex.Message);
        Assert.Contains("render", ex.Message);
    }

    // ----- Renderers (RegisterToString survives until group 7) -----

    [Fact]
    public void RegisterToString_RendersThroughRenderValue() {
        NativeCatalogue catalogue = Build(new Plugin("guid", r => r.RegisterToString("guid", v => "g!")));

        Assert.Equal("g!", catalogue.RenderValue(StructOf("guid", 1)));
    }

    [Fact]
    public void RegisterToString_Twice_Throws() {
        var ex = Assert.Throws<InvalidOperationException>(() => Build(
            new Plugin("first", r => r.RegisterToString("guid", v => "a")),
            new Plugin("second", r => r.RegisterToString("guid", v => "b"))));

        Assert.Contains("'guid'", ex.Message);
        Assert.Contains("'first'", ex.Message);
        Assert.Contains("'second'", ex.Message);
    }

    [Fact]
    public void RegisterToString_NullArguments_Throw() {
        Assert.Throws<ArgumentNullException>(() => Build(new Plugin("p", r => r.RegisterToString(null!, v => ""))));
        Assert.Throws<ArgumentNullException>(() => Build(new Plugin("p", r => r.RegisterToString("t", null!))));
    }

    [Fact]
    public void RenderValue_CapturedBeforeALaterPluginRegistersARenderer_StillUsesIt() {
        Func<GrobValue, string>? captured = null;
        NativeCatalogue catalogue = Build(
            new Plugin("formatAs", r => captured = r.RenderValue),
            new Plugin("guid", r => r.RegisterToString("guid", v => "g!")));

        Assert.Equal("g!", captured!(StructOf("guid", 1)));
        Assert.Equal("g!", catalogue.RenderValue(StructOf("guid", 1)));
    }

    [Fact]
    public void RenderValue_UnregisteredStruct_RendersStructurally() {
        NativeCatalogue catalogue = Build(new Plugin("p", r => { }));

        Assert.Equal("thing { value: 3 }", catalogue.RenderValue(StructOf("thing", 3)));
    }

    // ----- Builder lifecycle -----

    [Fact]
    public void Add_NullPlugin_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new NativeCatalogueBuilder().Add(null!));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Add_PluginWithoutAName_Throws(string? name) {
        var ex = Assert.Throws<ArgumentException>(() => new NativeCatalogueBuilder().Add(new Plugin(name!, r => { })));

        Assert.Equal("plugin", ex.ParamName);
    }

    [Fact]
    public void Add_ReturnsTheBuilder() {
        var builder = new NativeCatalogueBuilder();

        Assert.Same(builder, builder.Add(new Plugin("p", r => { })));
    }

    [Fact]
    public void Add_AfterBuild_Throws() {
        var builder = new NativeCatalogueBuilder();
        builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => builder.Add(new Plugin("p", r => { })));

        Assert.Contains("built", ex.Message);
    }

    [Fact]
    public void Build_Twice_Throws() {
        var builder = new NativeCatalogueBuilder();
        builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void Build_TwoPlugins_GlobalsHoldEveryRegistration() {
        NativeCatalogue catalogue = Build(
            new Plugin("math", r => {
                r.RegisterNative(Signature("math.sqrt"), Native("math.sqrt"));
                r.RegisterConstant("math.pi", GrobValue.FromFloat(3.14));
            }),
            new Plugin("io", r => r.RegisterNative("input", Native("input"))));

        Assert.Equal(3, catalogue.Globals.Count);
        Assert.Equal(["math.sqrt", "math.pi", "input"], catalogue.Globals.Keys);
    }
}
