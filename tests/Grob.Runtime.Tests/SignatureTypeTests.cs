using Grob.Core;

using Xunit;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for <see cref="SignatureType"/>, the closed union a native signature uses for
/// a parameter or return type (D-419 Decision 2, D-426 Decision 4). The three members
/// are a concrete Grob type, a reference to one of the signature's own type parameters
/// and an array of such a reference. The concrete member validates its own shape so an
/// invalid combination of fields cannot be constructed.
/// </summary>
public sealed class SignatureTypeTests {
    // ----- Concrete: happy paths -----

    [Fact]
    public void Of_PrimitiveType_CarriesTypeOnly() {
        SignatureType.Concrete type = SignatureType.Of(GrobType.Int);

        Assert.Equal(GrobType.Int, type.Type);
        Assert.Null(type.NamedTypeName);
        Assert.Null(type.ElementType);
    }

    [Fact]
    public void Named_StructType_CarriesNamedTypeName() {
        SignatureType.Concrete type = SignatureType.Named(GrobType.Struct, "date");

        Assert.Equal(GrobType.Struct, type.Type);
        Assert.Equal("date", type.NamedTypeName);
    }

    [Fact]
    public void Named_NullableStructType_CarriesNamedTypeName() {
        SignatureType.Concrete type = SignatureType.Named(GrobType.NullableStruct, "guid");

        Assert.Equal(GrobType.NullableStruct, type.Type);
        Assert.Equal("guid", type.NamedTypeName);
    }

    [Fact]
    public void Concrete_StructWithoutName_MeansAnyStruct() {
        SignatureType.Concrete type = SignatureType.Of(GrobType.Struct);

        Assert.Null(type.NamedTypeName);
    }

    [Fact]
    public void ArrayOf_ElementType_CarriesElementType() {
        SignatureType.Concrete type = SignatureType.ArrayOf(SignatureType.Of(GrobType.String));

        Assert.Equal(GrobType.Array, type.Type);
        Assert.Equal(SignatureType.Of(GrobType.String), type.ElementType);
    }

    [Fact]
    public void Concrete_NullableArrayWithElementType_IsPermitted() {
        var type = new SignatureType.Concrete(GrobType.NullableArray, elementType: SignatureType.Of(GrobType.Int));

        Assert.Equal(GrobType.NullableArray, type.Type);
        Assert.Equal(GrobType.Int, type.ElementType!.Type);
    }

    [Fact]
    public void Concrete_ArrayWithoutElementType_MeansAnyArray() {
        SignatureType.Concrete type = SignatureType.Of(GrobType.Array);

        Assert.Null(type.ElementType);
    }

    [Fact]
    public void Concrete_NestedArray_IsPermitted() {
        SignatureType.Concrete inner = SignatureType.ArrayOf(SignatureType.Of(GrobType.String));
        SignatureType.Concrete outer = SignatureType.ArrayOf(inner);

        Assert.Equal(inner, outer.ElementType);
    }

    [Fact]
    public void Concrete_SameShape_AreEqual() {
        var a = new SignatureType.Concrete(GrobType.Array, elementType: SignatureType.Named(GrobType.Struct, "date"));
        var b = new SignatureType.Concrete(GrobType.Array, elementType: SignatureType.Named(GrobType.Struct, "date"));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Concrete_DifferentNamedTypeName_AreNotEqual() =>
        Assert.NotEqual(SignatureType.Named(GrobType.Struct, "date"), SignatureType.Named(GrobType.Struct, "guid"));

    [Fact]
    public void Concrete_NilType_IsPermittedForVoidReturns() =>
        Assert.Equal(GrobType.Nil, SignatureType.Of(GrobType.Nil).Type);

    [Theory]
    [InlineData(GrobType.Int, false)]
    [InlineData(GrobType.NullableInt, true)]
    [InlineData(GrobType.String, false)]
    [InlineData(GrobType.NullableString, true)]
    [InlineData(GrobType.Array, false)]
    [InlineData(GrobType.NullableArray, true)]
    [InlineData(GrobType.Map, false)]
    [InlineData(GrobType.NullableMap, true)]
    [InlineData(GrobType.Struct, false)]
    [InlineData(GrobType.NullableStruct, true)]
    [InlineData(GrobType.AnonStruct, false)]
    [InlineData(GrobType.NullableAnonStruct, true)]
    [InlineData(GrobType.Nil, false)]
    public void Concrete_IsNullable_ReflectsTheTag(GrobType type, bool expected) =>
        Assert.Equal(expected, SignatureType.Of(type).IsNullable);

    // ----- Concrete: rejected shapes -----

    [Theory]
    [InlineData(GrobType.Unknown)]
    [InlineData(GrobType.Error)]
    public void Concrete_UnresolvedType_Throws(GrobType type) {
        var ex = Assert.Throws<ArgumentException>(() => SignatureType.Of(type));

        Assert.Equal("type", ex.ParamName);
        Assert.Contains(type.ToString(), ex.Message);
    }

    [Theory]
    [InlineData(GrobType.Function)]
    [InlineData(GrobType.NullableFunction)]
    public void Concrete_FunctionType_Throws(GrobType type) {
        var ex = Assert.Throws<ArgumentException>(() => SignatureType.Of(type));

        Assert.Equal("type", ex.ParamName);
        Assert.Contains("function", ex.Message);
    }

    [Fact]
    public void Concrete_UndefinedEnumValue_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => SignatureType.Of((GrobType)999));

        Assert.Equal("type", ex.ParamName);
    }

    [Theory]
    [InlineData(GrobType.Int)]
    [InlineData(GrobType.String)]
    [InlineData(GrobType.Array)]
    [InlineData(GrobType.AnonStruct)]
    public void Concrete_NamedTypeNameOnNonStruct_Throws(GrobType type) {
        var ex = Assert.Throws<ArgumentException>(() => SignatureType.Named(type, "date"));

        Assert.Equal("namedTypeName", ex.ParamName);
        Assert.Contains("Struct", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("my type")]
    [InlineData("1date")]
    [InlineData("date.time")]
    public void Concrete_NamedTypeNameNotAnIdentifier_Throws(string name) {
        var ex = Assert.Throws<ArgumentException>(() => SignatureType.Named(GrobType.Struct, name));

        Assert.Equal("namedTypeName", ex.ParamName);
        Assert.Contains("identifier", ex.Message);
    }

    [Theory]
    [InlineData(GrobType.Int)]
    [InlineData(GrobType.Struct)]
    [InlineData(GrobType.Map)]
    public void Concrete_ElementTypeOnNonArray_Throws(GrobType type) {
        var ex = Assert.Throws<ArgumentException>(
            () => new SignatureType.Concrete(type, elementType: SignatureType.Of(GrobType.Int)));

        Assert.Equal("elementType", ex.ParamName);
        Assert.Contains("Array", ex.Message);
    }

    [Fact]
    public void Concrete_NilElementType_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => SignatureType.ArrayOf(SignatureType.Of(GrobType.Nil)));

        Assert.Equal("elementType", ex.ParamName);
        Assert.Contains("nil", ex.Message);
    }

    [Fact]
    public void ArrayOf_NullElement_Throws() =>
        Assert.Throws<ArgumentNullException>(() => SignatureType.ArrayOf(null!));

    // ----- Type-parameter references -----

    [Fact]
    public void TypeParameterReference_CarriesName() =>
        Assert.Equal("T", new SignatureType.TypeParameterReference("T").Name);

    [Fact]
    public void TypeParameterArray_CarriesName() =>
        Assert.Equal("T", new SignatureType.TypeParameterArray("T").Name);

    [Fact]
    public void TypeParameterReference_SameName_AreEqual() =>
        Assert.Equal(new SignatureType.TypeParameterReference("T"), new SignatureType.TypeParameterReference("T"));

    [Fact]
    public void TypeParameterReference_IsNotEqualToArrayOfSameName() {
        SignatureType reference = new SignatureType.TypeParameterReference("T");
        SignatureType array = new SignatureType.TypeParameterArray("T");

        Assert.NotEqual(reference, array);
    }

    [Theory]
    [InlineData("")]
    [InlineData("T U")]
    [InlineData("1T")]
    [InlineData("T.U")]
    public void TypeParameterReference_NameNotAnIdentifier_Throws(string name) {
        var ex = Assert.Throws<ArgumentException>(() => new SignatureType.TypeParameterReference(name));

        Assert.Equal("name", ex.ParamName);
        Assert.Contains("identifier", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("T U")]
    [InlineData("1T")]
    public void TypeParameterArray_NameNotAnIdentifier_Throws(string name) {
        var ex = Assert.Throws<ArgumentException>(() => new SignatureType.TypeParameterArray(name));

        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void TypeParameterReference_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new SignatureType.TypeParameterReference(null!));

    [Fact]
    public void TypeParameterArray_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new SignatureType.TypeParameterArray(null!));
}
