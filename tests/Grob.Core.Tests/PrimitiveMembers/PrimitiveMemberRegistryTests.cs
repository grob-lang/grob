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
    [InlineData(GrobType.Int)]
    [InlineData(GrobType.Float)]
    [InlineData(GrobType.Bool)]
    [InlineData(GrobType.Array)]
    [InlineData(GrobType.Struct)]
    public void TryGet_UnregisteredReceiver_ReturnsFalse(GrobType receiverType) {
        Assert.False(PrimitiveMemberRegistry.TryGet(receiverType, out _));
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

    [Fact]
    public void StringEntry_HasExactlyTwoPropertiesAndNineteenMethods() {
        Assert.Equal(2, PrimitiveMemberRegistry.String.Properties.Count);
        Assert.Equal(19, PrimitiveMemberRegistry.String.Methods.Count);
    }

    [Fact]
    public void AllQualifiedNativeNames_ContainsEveryStringMemberOnce() {
        var expected = PrimitiveMemberRegistry.String.Properties.Values.Select(p => p.QualifiedNativeName)
            .Concat(PrimitiveMemberRegistry.String.Methods.Values.Select(m => m.QualifiedNativeName))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var actual = PrimitiveMemberRegistry.AllQualifiedNativeNames.OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(21, actual.Count);
        Assert.Equal(actual.Count, actual.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(expected, actual);
    }
}
