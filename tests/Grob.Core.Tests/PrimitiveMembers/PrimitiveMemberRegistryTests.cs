using Grob.Core.PrimitiveMembers;
using Xunit;

namespace Grob.Core.Tests.PrimitiveMembers;

/// <summary>
/// Direct unit coverage of <see cref="PrimitiveMemberRegistry"/>'s <c>string</c> entry
/// (Sprint 9, primitive instance-method dispatch) — a compile-time-only twin table
/// (no executable <c>Bind</c>, unlike <c>NamedTypeRegistry</c>: primitive dispatch
/// rewrites to a qualified native call rather than binding a receiver-closed
/// <c>NativeFunction</c>), asserting every declared property/method signature and the
/// flattened qualified-name list the agreement test diffs against live registration.
/// </summary>
public sealed class PrimitiveMemberRegistryTests {
    [Fact]
    public void TryGet_StringReceiver_ReturnsStringEntry() {
        Assert.True(PrimitiveMemberRegistry.TryGet(GrobType.String, out PrimitiveMemberEntry entry));
        Assert.Equal(GrobType.String, entry.ReceiverType);
        Assert.Same(PrimitiveMemberRegistry.String, entry);
    }

    [Theory]
    [InlineData(GrobType.Array)]
    [InlineData(GrobType.Struct)]
    public void TryGet_UnregisteredReceiver_ReturnsFalse(GrobType receiverType) {
        Assert.False(PrimitiveMemberRegistry.TryGet(receiverType, out _));
    }

    [Theory]
    [InlineData(GrobType.Int)]
    [InlineData(GrobType.Float)]
    [InlineData(GrobType.Bool)]
    public void TryGet_NumericOrBoolReceiver_ReturnsRegisteredEntry(GrobType receiverType) {
        Assert.True(PrimitiveMemberRegistry.TryGet(receiverType, out PrimitiveMemberEntry entry));
        Assert.Equal(receiverType, entry.ReceiverType);
    }

    [Theory]
    [InlineData("length", GrobType.Int, "string.length")]
    [InlineData("isEmpty", GrobType.Bool, "string.isEmpty")]
    public void StringProperties_HaveDeclaredSignature(string name, GrobType type, string qualifiedNativeName) {
        PrimitiveMemberProperty property = PrimitiveMemberRegistry.String.Properties[name];
        Assert.Equal(name, property.Name);
        Assert.Equal(type, property.Type);
        Assert.Equal(qualifiedNativeName, property.QualifiedNativeName);
    }

    [Theory]
    [InlineData("toInt", new GrobType[] { }, GrobType.NullableInt, "string.toInt")]
    [InlineData("toFloat", new GrobType[] { }, GrobType.NullableFloat, "string.toFloat")]
    [InlineData("trim", new GrobType[] { }, GrobType.String, "string.trim")]
    [InlineData("trimStart", new GrobType[] { }, GrobType.String, "string.trimStart")]
    [InlineData("trimEnd", new GrobType[] { }, GrobType.String, "string.trimEnd")]
    [InlineData("upper", new GrobType[] { }, GrobType.String, "string.upper")]
    [InlineData("lower", new GrobType[] { }, GrobType.String, "string.lower")]
    [InlineData("split", new[] { GrobType.String }, GrobType.Array, "string.split")]
    [InlineData("contains", new[] { GrobType.String }, GrobType.Bool, "string.contains")]
    [InlineData("startsWith", new[] { GrobType.String }, GrobType.Bool, "string.startsWith")]
    [InlineData("endsWith", new[] { GrobType.String }, GrobType.Bool, "string.endsWith")]
    [InlineData("replace", new[] { GrobType.String, GrobType.String }, GrobType.String, "string.replace")]
    [InlineData("indexOf", new[] { GrobType.String }, GrobType.Int, "string.indexOf")]
    [InlineData("lastIndexOf", new[] { GrobType.String }, GrobType.Int, "string.lastIndexOf")]
    [InlineData("substring", new[] { GrobType.Int, GrobType.Int }, GrobType.String, "string.substring")]
    [InlineData("repeat", new[] { GrobType.Int }, GrobType.String, "string.repeat")]
    [InlineData("left", new[] { GrobType.Int }, GrobType.String, "string.left")]
    [InlineData("right", new[] { GrobType.Int }, GrobType.String, "string.right")]
    [InlineData("toString", new GrobType[] { }, GrobType.String, "string.toString")]
    public void StringMethods_HaveDeclaredSignature(
            string name, GrobType[] parameterTypes, GrobType returnType, string qualifiedNativeName) {
        PrimitiveMemberMethod method = PrimitiveMemberRegistry.String.Methods[name];
        Assert.Equal(name, method.Name);
        Assert.Equal(parameterTypes, method.ParameterTypes);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(qualifiedNativeName, method.QualifiedNativeName);
        Assert.Null(method.ParameterDefaults);
    }

    // -----------------------------------------------------------------------
    // padLeft/padRight/truncate — the three default-parameter methods D-358's
    // NativeDefaultArgumentFill mechanism unblocks (D-365).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("padLeft", new[] { GrobType.Int, GrobType.String }, GrobType.String, "string.padLeft")]
    [InlineData("padRight", new[] { GrobType.Int, GrobType.String }, GrobType.String, "string.padRight")]
    [InlineData("truncate", new[] { GrobType.Int, GrobType.String }, GrobType.String, "string.truncate")]
    public void StringMethods_WithDefaultParameter_HaveDeclaredSignature(
            string name, GrobType[] parameterTypes, GrobType returnType, string qualifiedNativeName) {
        PrimitiveMemberMethod method = PrimitiveMemberRegistry.String.Methods[name];
        Assert.Equal(name, method.Name);
        Assert.Equal(parameterTypes, method.ParameterTypes);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(qualifiedNativeName, method.QualifiedNativeName);
    }

    [Theory]
    [InlineData("padLeft", " ")]
    [InlineData("padRight", " ")]
    [InlineData("truncate", "...")]
    public void StringMethods_WithDefaultParameter_HaveDeclaredParameterDefault(string name, string expectedDefault) {
        PrimitiveMemberMethod method = PrimitiveMemberRegistry.String.Methods[name];
        Assert.NotNull(method.ParameterDefaults);
        Assert.Equal(2, method.ParameterDefaults!.Count);
        Assert.Null(method.ParameterDefaults[0]);
        Assert.Equal(GrobValue.FromString(expectedDefault), method.ParameterDefaults[1]);
    }

    [Fact]
    public void StringEntry_HasExactlyTwoPropertiesAndTwentyTwoMethods() {
        Assert.Equal(2, PrimitiveMemberRegistry.String.Properties.Count);
        Assert.Equal(22, PrimitiveMemberRegistry.String.Methods.Count);
    }

    [Fact]
    public void AllQualifiedNativeNames_ContainsEveryMemberOnce() {
        var expected = PrimitiveMemberRegistry.String.Properties.Values.Select(p => p.QualifiedNativeName)
            .Concat(PrimitiveMemberRegistry.String.Methods.Values.Select(m => m.QualifiedNativeName))
            .Concat(PrimitiveMemberRegistry.Int.Methods.Values.Select(m => m.QualifiedNativeName))
            .Concat(PrimitiveMemberRegistry.Float.Methods.Values.Select(m => m.QualifiedNativeName))
            .Concat(PrimitiveMemberRegistry.Bool.Methods.Values.Select(m => m.QualifiedNativeName))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var actual = PrimitiveMemberRegistry.AllQualifiedNativeNames.OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(24 + 4 + 8 + 1, actual.Count);
        Assert.Equal(actual.Count, actual.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(expected, actual);
    }

    // -----------------------------------------------------------------------
    // int — toString/toFloat/abs/format(pattern). No properties.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("toString", new GrobType[] { }, GrobType.String, "int.toString")]
    [InlineData("toFloat", new GrobType[] { }, GrobType.Float, "int.toFloat")]
    [InlineData("abs", new GrobType[] { }, GrobType.Int, "int.abs")]
    [InlineData("format", new[] { GrobType.String }, GrobType.String, "int.format")]
    public void IntMethods_HaveDeclaredSignature(
            string name, GrobType[] parameterTypes, GrobType returnType, string qualifiedNativeName) {
        PrimitiveMemberMethod method = PrimitiveMemberRegistry.Int.Methods[name];
        Assert.Equal(name, method.Name);
        Assert.Equal(parameterTypes, method.ParameterTypes);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(qualifiedNativeName, method.QualifiedNativeName);
        Assert.Null(method.ParameterDefaults);
    }

    [Fact]
    public void IntEntry_HasNoPropertiesAndExactlyFourMethods() {
        Assert.Equal(GrobType.Int, PrimitiveMemberRegistry.Int.ReceiverType);
        Assert.Empty(PrimitiveMemberRegistry.Int.Properties);
        Assert.Equal(4, PrimitiveMemberRegistry.Int.Methods.Count);
    }

    // -----------------------------------------------------------------------
    // float — toString/toInt/round/roundTo(decimals)/floor/ceil/abs/format(pattern).
    // No properties.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("toString", new GrobType[] { }, GrobType.String, "float.toString")]
    [InlineData("toInt", new GrobType[] { }, GrobType.Int, "float.toInt")]
    [InlineData("round", new GrobType[] { }, GrobType.Int, "float.round")]
    [InlineData("roundTo", new[] { GrobType.Int }, GrobType.Float, "float.roundTo")]
    [InlineData("floor", new GrobType[] { }, GrobType.Int, "float.floor")]
    [InlineData("ceil", new GrobType[] { }, GrobType.Int, "float.ceil")]
    [InlineData("abs", new GrobType[] { }, GrobType.Float, "float.abs")]
    [InlineData("format", new[] { GrobType.String }, GrobType.String, "float.format")]
    public void FloatMethods_HaveDeclaredSignature(
            string name, GrobType[] parameterTypes, GrobType returnType, string qualifiedNativeName) {
        PrimitiveMemberMethod method = PrimitiveMemberRegistry.Float.Methods[name];
        Assert.Equal(name, method.Name);
        Assert.Equal(parameterTypes, method.ParameterTypes);
        Assert.Equal(returnType, method.ReturnType);
        Assert.Equal(qualifiedNativeName, method.QualifiedNativeName);
        Assert.Null(method.ParameterDefaults);
    }

    [Fact]
    public void FloatEntry_HasNoPropertiesAndExactlyEightMethods() {
        Assert.Equal(GrobType.Float, PrimitiveMemberRegistry.Float.ReceiverType);
        Assert.Empty(PrimitiveMemberRegistry.Float.Properties);
        Assert.Equal(8, PrimitiveMemberRegistry.Float.Methods.Count);
    }

    // -----------------------------------------------------------------------
    // bool — toString only. No properties.
    // -----------------------------------------------------------------------

    [Fact]
    public void BoolMethods_ToString_HasDeclaredSignature() {
        PrimitiveMemberMethod method = PrimitiveMemberRegistry.Bool.Methods["toString"];
        Assert.Equal("toString", method.Name);
        Assert.Empty(method.ParameterTypes);
        Assert.Equal(GrobType.String, method.ReturnType);
        Assert.Equal("bool.toString", method.QualifiedNativeName);
        Assert.Null(method.ParameterDefaults);
    }

    [Fact]
    public void BoolEntry_HasNoPropertiesAndExactlyOneMethod() {
        Assert.Equal(GrobType.Bool, PrimitiveMemberRegistry.Bool.ReceiverType);
        Assert.Empty(PrimitiveMemberRegistry.Bool.Properties);
        Assert.Single(PrimitiveMemberRegistry.Bool.Methods);
    }
}
