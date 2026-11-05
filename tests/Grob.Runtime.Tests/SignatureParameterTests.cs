using Grob.Core;

using Xunit;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for <see cref="SignatureParameter"/>: one positional parameter of a native
/// signature. The four accreted shapes D-426 Decision 4 collapses — a variadic tail, a
/// trailing default, a nominal struct parameter and a named-type self parameter — are
/// all expressed here as a flag, an optional value and a concrete type with a name.
/// </summary>
public sealed class SignatureParameterTests {
    private static readonly SignatureType.Concrete Int = SignatureType.Of(GrobType.Int);
    private static readonly SignatureType.Concrete String = SignatureType.Of(GrobType.String);

    // ----- Happy paths -----

    [Fact]
    public void Constructor_NameAndType_SetsDefaults() {
        var parameter = new SignatureParameter("n", Int);

        Assert.Equal("n", parameter.Name);
        Assert.Equal(Int, parameter.Type);
        Assert.False(parameter.IsVariadic);
        Assert.False(parameter.HasDefault);
        Assert.Null(parameter.Default);
    }

    [Fact]
    public void Constructor_Variadic_SetsIsVariadic() {
        var parameter = new SignatureParameter("parts", String, isVariadic: true);

        Assert.True(parameter.IsVariadic);
    }

    [Theory]
    [InlineData(GrobType.Int, GrobValueKind.Int)]
    [InlineData(GrobType.NullableInt, GrobValueKind.Int)]
    [InlineData(GrobType.Float, GrobValueKind.Float)]
    [InlineData(GrobType.NullableFloat, GrobValueKind.Float)]
    [InlineData(GrobType.String, GrobValueKind.String)]
    [InlineData(GrobType.NullableString, GrobValueKind.String)]
    [InlineData(GrobType.Bool, GrobValueKind.Bool)]
    [InlineData(GrobType.NullableBool, GrobValueKind.Bool)]
    public void Constructor_ScalarDefaultMatchingType_IsPermitted(GrobType type, GrobValueKind kind) {
        GrobValue value = Sample(kind);

        var parameter = new SignatureParameter("p", SignatureType.Of(type), defaultValue: value);

        Assert.True(parameter.HasDefault);
        Assert.Equal(value, parameter.Default);
    }

    [Theory]
    [InlineData(GrobType.NullableInt)]
    [InlineData(GrobType.NullableString)]
    [InlineData(GrobType.NullableArray)]
    [InlineData(GrobType.NullableMap)]
    [InlineData(GrobType.NullableStruct)]
    [InlineData(GrobType.NullableAnonStruct)]
    public void Constructor_NilDefaultOnNullableType_IsPermitted(GrobType type) {
        var parameter = new SignatureParameter("p", SignatureType.Of(type), defaultValue: GrobValue.Nil);

        Assert.True(parameter.HasDefault);
        Assert.True(parameter.Default!.Value.IsNil);
    }

    [Fact]
    public void Constructor_TypeParameterTyped_IsPermitted() {
        var parameter = new SignatureParameter("value", new SignatureType.TypeParameterReference("T"));

        Assert.IsType<SignatureType.TypeParameterReference>(parameter.Type);
    }

    [Fact]
    public void Constructor_NominalStructType_IsPermitted() {
        var parameter = new SignatureParameter("other", SignatureType.Named(GrobType.Struct, "date"));

        Assert.Equal("date", ((SignatureType.Concrete)parameter.Type).NamedTypeName);
    }

    [Fact]
    public void Equality_SameShape_AreEqual() =>
        Assert.Equal(
            new SignatureParameter("n", Int, defaultValue: GrobValue.FromInt(1)),
            new SignatureParameter("n", Int, defaultValue: GrobValue.FromInt(1)));

    // ----- Rejected shapes -----

    [Fact]
    public void Constructor_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new SignatureParameter(null!, Int));

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("1a")]
    [InlineData("a.b")]
    public void Constructor_NameNotAnIdentifier_Throws(string name) {
        var ex = Assert.Throws<ArgumentException>(() => new SignatureParameter(name, Int));

        Assert.Equal("name", ex.ParamName);
        Assert.Contains("identifier", ex.Message);
    }

    [Fact]
    public void Constructor_NullType_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new SignatureParameter("n", null!));

    [Fact]
    public void Constructor_NilType_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => new SignatureParameter("n", SignatureType.Of(GrobType.Nil)));

        Assert.Equal("type", ex.ParamName);
        Assert.Contains("nil", ex.Message);
    }

    [Fact]
    public void Constructor_VariadicWithDefault_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new SignatureParameter("parts", String, isVariadic: true, defaultValue: GrobValue.FromString("")));

        Assert.Equal("defaultValue", ex.ParamName);
        Assert.Contains("variadic", ex.Message);
    }

    [Fact]
    public void Constructor_TypeParameterTypedWithDefault_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new SignatureParameter("value", new SignatureType.TypeParameterReference("T"), defaultValue: GrobValue.Nil));

        Assert.Equal("defaultValue", ex.ParamName);
        Assert.Contains("type parameter", ex.Message);
    }

    [Theory]
    [InlineData(GrobType.Int, GrobValueKind.String)]
    [InlineData(GrobType.Int, GrobValueKind.Float)]
    [InlineData(GrobType.Int, GrobValueKind.Bool)]
    [InlineData(GrobType.String, GrobValueKind.Int)]
    [InlineData(GrobType.Float, GrobValueKind.Int)]
    [InlineData(GrobType.Bool, GrobValueKind.String)]
    [InlineData(GrobType.NullableInt, GrobValueKind.String)]
    public void Constructor_DefaultKindMismatch_Throws(GrobType type, GrobValueKind kind) {
        var ex = Assert.Throws<ArgumentException>(
            () => new SignatureParameter("p", SignatureType.Of(type), defaultValue: Sample(kind)));

        Assert.Equal("defaultValue", ex.ParamName);
        Assert.Contains(kind.ToString(), ex.Message);
        Assert.Contains(type.ToString(), ex.Message);
    }

    [Theory]
    [InlineData(GrobType.Int)]
    [InlineData(GrobType.String)]
    [InlineData(GrobType.Array)]
    [InlineData(GrobType.Struct)]
    public void Constructor_NilDefaultOnNonNullableType_Throws(GrobType type) {
        var ex = Assert.Throws<ArgumentException>(
            () => new SignatureParameter("p", SignatureType.Of(type), defaultValue: GrobValue.Nil));

        Assert.Equal("defaultValue", ex.ParamName);
        Assert.Contains("nullable", ex.Message);
    }

    [Theory]
    [InlineData(GrobType.Array, GrobValueKind.Array)]
    [InlineData(GrobType.Map, GrobValueKind.Map)]
    [InlineData(GrobType.Struct, GrobValueKind.Struct)]
    [InlineData(GrobType.Int, GrobValueKind.Function)]
    public void Constructor_CompositeDefault_Throws(GrobType type, GrobValueKind kind) {
        var ex = Assert.Throws<ArgumentException>(
            () => new SignatureParameter("p", SignatureType.Of(type), defaultValue: Sample(kind)));

        Assert.Equal("defaultValue", ex.ParamName);
        Assert.Contains("scalar", ex.Message);
    }

    private static GrobValue Sample(GrobValueKind kind) => kind switch {
        GrobValueKind.Nil => GrobValue.Nil,
        GrobValueKind.Bool => GrobValue.FromBool(true),
        GrobValueKind.Int => GrobValue.FromInt(42),
        GrobValueKind.Float => GrobValue.FromFloat(1.5),
        GrobValueKind.String => GrobValue.FromString("x"),
        GrobValueKind.Array => GrobValue.FromArray(new GrobArray()),
        GrobValueKind.Map => GrobValue.FromMap(new GrobMap()),
        GrobValueKind.Struct => GrobValue.FromStruct(new GrobStruct("t", [])),
        GrobValueKind.Function => GrobValue.FromFunction(new NativeFunction("f", 0, (_, _) => GrobValue.Nil)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
