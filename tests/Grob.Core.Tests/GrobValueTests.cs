using System.Runtime.CompilerServices;
using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public sealed class GrobValueTests {
    // ----- Default value -----

    [Fact]
    public void Default_IsNil() {
        var v = default(GrobValue);

        Assert.True(v.IsNil);
        Assert.Equal(GrobValueKind.Nil, v.Kind);
    }

    [Fact]
    public void Default_EqualsNilSingleton() {
        Assert.Equal(default(GrobValue), GrobValue.Nil);
        Assert.True(default(GrobValue) == GrobValue.Nil);
    }

    // ----- Construction round-trips -----

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bool_RoundTrip(bool input) {
        var v = GrobValue.FromBool(input);

        Assert.Equal(GrobValueKind.Bool, v.Kind);
        Assert.True(v.IsBool);
        Assert.Equal(input, v.AsBool());
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void Int_RoundTrip(long input) {
        var v = GrobValue.FromInt(input);

        Assert.Equal(GrobValueKind.Int, v.Kind);
        Assert.True(v.IsInt);
        Assert.Equal(input, v.AsInt());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    [InlineData(-42.0)]
    [InlineData(double.MaxValue)]
    [InlineData(double.MinValue)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Float_RoundTrip(double input) {
        var v = GrobValue.FromFloat(input);

        Assert.Equal(GrobValueKind.Float, v.Kind);
        Assert.True(v.IsFloat);
        Assert.Equal(input, v.AsFloat());
    }

    [Fact]
    public void Float_NaN_RoundTrip() {
        var v = GrobValue.FromFloat(double.NaN);

        Assert.Equal(GrobValueKind.Float, v.Kind);
        Assert.True(double.IsNaN(v.AsFloat()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("hello")]
    [InlineData("日本語")]      // multi-byte UTF-8
    public void String_RoundTrip(string input) {
        var v = GrobValue.FromString(input);

        Assert.Equal(GrobValueKind.String, v.Kind);
        Assert.True(v.IsString);
        Assert.Equal(input, v.AsString());
    }

    [Fact]
    public void Array_RoundTrip() {
        var arr = new GrobArray();
        var v = GrobValue.FromArray(arr);

        Assert.Equal(GrobValueKind.Array, v.Kind);
        Assert.True(v.IsArray);
        Assert.Same(arr, v.AsArray());
    }

    [Fact]
    public void Map_RoundTrip() {
        var map = new GrobMap();
        var v = GrobValue.FromMap(map);

        Assert.Equal(GrobValueKind.Map, v.Kind);
        Assert.True(v.IsMap);
        Assert.Same(map, v.AsMap());
    }

    [Fact]
    public void Struct_RoundTrip() {
        var s = new GrobStruct("Point");
        var v = GrobValue.FromStruct(s);

        Assert.Equal(GrobValueKind.Struct, v.Kind);
        Assert.True(v.IsStruct);
        Assert.Same(s, v.AsStruct());
    }

    [Fact]
    public void Function_RoundTrip() {
        var fn = new BytecodeFunction("add", 2, new Chunk());
        var v = GrobValue.FromFunction(fn);

        Assert.Equal(GrobValueKind.Function, v.Kind);
        Assert.True(v.IsFunction);
        Assert.Same(fn, v.AsFunction());
    }

    // ----- Discrimination -----

    [Fact]
    public void IsX_OnlyTrueForOwnKind() {
        GrobValue nil = GrobValue.Nil;
        GrobValue b = GrobValue.FromBool(true);
        GrobValue i = GrobValue.FromInt(1);
        GrobValue f = GrobValue.FromFloat(1.0);
        GrobValue s = GrobValue.FromString("x");
        GrobValue arr = GrobValue.FromArray(new GrobArray());
        GrobValue map = GrobValue.FromMap(new GrobMap());
        GrobValue st = GrobValue.FromStruct(new GrobStruct("T"));
        GrobValue fn = GrobValue.FromFunction(new BytecodeFunction("f", 0, new Chunk()));

        // IsNil
        Assert.True(nil.IsNil);
        Assert.False(b.IsNil); Assert.False(i.IsNil); Assert.False(f.IsNil);

        // IsBool
        Assert.True(b.IsBool);
        Assert.False(nil.IsBool); Assert.False(i.IsBool);

        // IsInt
        Assert.True(i.IsInt);
        Assert.False(nil.IsInt); Assert.False(b.IsInt); Assert.False(f.IsInt);

        // IsFloat
        Assert.True(f.IsFloat);
        Assert.False(nil.IsFloat); Assert.False(i.IsFloat);

        // IsString
        Assert.True(s.IsString);
        Assert.False(nil.IsString); Assert.False(i.IsString);

        // IsArray
        Assert.True(arr.IsArray);
        Assert.False(nil.IsArray); Assert.False(s.IsArray);

        // IsMap
        Assert.True(map.IsMap);
        Assert.False(nil.IsMap); Assert.False(arr.IsMap);

        // IsStruct
        Assert.True(st.IsStruct);
        Assert.False(nil.IsStruct); Assert.False(map.IsStruct);

        // IsFunction
        Assert.True(fn.IsFunction);
        Assert.False(nil.IsFunction); Assert.False(st.IsFunction);
    }

    // ----- Kind-mismatch strict accessors -----

    [Fact]
    public void StrictAccessor_WrongKind_ThrowsGrobInternalException() {
        var v = GrobValue.FromInt(42);

        var ex = Assert.Throws<GrobInternalException>(() => v.AsString());
        Assert.Contains("Int", ex.Message);
        Assert.Contains("String", ex.Message);
    }

    [Fact]
    public void TryAccessor_WrongKind_ReturnsFalse_NoException() {
        var v = GrobValue.FromInt(42);

        Assert.False(v.TryAsString(out _));
        Assert.False(v.TryAsBool(out _));
        Assert.False(v.TryAsFloat(out _));
    }

    [Fact]
    public void TryAccessor_CorrectKind_ReturnsTrueAndValue() {
        var v = GrobValue.FromInt(99);

        Assert.True(v.TryAsInt(out var result));
        Assert.Equal(99, result);
    }

    // ----- Equality — same kind -----

    [Fact]
    public void Nil_EqualsNil() {
        var nil1 = GrobValue.Nil;
        var nil2 = GrobValue.Nil;
        Assert.Equal(nil1, nil2);
        Assert.True(nil1 == nil2);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bool_EqualsSameBool(bool v) {
        Assert.Equal(GrobValue.FromBool(v), GrobValue.FromBool(v));
    }

    [Fact]
    public void Bool_DifferentValue_NotEqual() {
        Assert.NotEqual(GrobValue.FromBool(true), GrobValue.FromBool(false));
        Assert.True(GrobValue.FromBool(true) != GrobValue.FromBool(false));
    }

    [Fact]
    public void Int_EqualsSameValue() {
        Assert.Equal(GrobValue.FromInt(42), GrobValue.FromInt(42));
    }

    [Fact]
    public void Float_EqualsSameValue() {
        Assert.Equal(GrobValue.FromFloat(3.14), GrobValue.FromFloat(3.14));
    }

    [Fact]
    public void Float_PositiveZero_EqualsNegativeZero() {
        Assert.Equal(GrobValue.FromFloat(0.0), GrobValue.FromFloat(-0.0));
        Assert.True(GrobValue.FromFloat(0.0) == GrobValue.FromFloat(-0.0));
    }

    [Fact]
    public void Float_NaN_NotEqualToItself_ViaOperator() {
        var nan1 = GrobValue.FromFloat(double.NaN);
        var nan2 = GrobValue.FromFloat(double.NaN);
        Assert.True(nan1 != nan2);
        Assert.False(nan1 == nan2);
    }

    [Fact]
    public void Float_NaN_EqualToItself_ViaEqualsMethod() {
        var nan1 = GrobValue.FromFloat(double.NaN);
        var nan2 = GrobValue.FromFloat(double.NaN);
        Assert.True(nan1.Equals(nan2));
    }

    [Fact]
    public void String_OrdinalEquality() {
        Assert.Equal(GrobValue.FromString("hello"), GrobValue.FromString("hello"));
        Assert.NotEqual(GrobValue.FromString("Hello"), GrobValue.FromString("hello"));
    }

    [Fact]
    public void Array_ReferenceEquality() {
        var arr = new GrobArray();
        var v1 = GrobValue.FromArray(arr);
        var v2 = GrobValue.FromArray(arr);
        var v3 = GrobValue.FromArray(new GrobArray());   // different instance, same logical content

        Assert.Equal(v1, v2);
        Assert.NotEqual(v1, v3);
    }

    [Fact]
    public void Struct_DelegatesToGrobStructEquals() {
        var s1 = new GrobStruct("Point");
        s1.SetField("x", GrobValue.FromInt(1));
        var s2 = new GrobStruct("Point");
        s2.SetField("x", GrobValue.FromInt(1));
        var s3 = new GrobStruct("Point");
        s3.SetField("x", GrobValue.FromInt(2));

        Assert.Equal(GrobValue.FromStruct(s1), GrobValue.FromStruct(s2));
        Assert.NotEqual(GrobValue.FromStruct(s1), GrobValue.FromStruct(s3));
    }

    [Fact]
    public void Struct_ContainingNaN_IsReflexivelyEqual_ViaEquals() {
        // .NET Equals/GetHashCode contract: x.Equals(x) MUST be true, so that
        // structs containing NaN can be used as collection keys. IEEE 754 NaN
        // semantics belong on operator==, not on Equals.
        var s = new GrobStruct("Box");
        s.SetField("f", GrobValue.FromFloat(double.NaN));

        Assert.True(s.Equals(s));
        Assert.Equal(s.GetHashCode(), s.GetHashCode());
    }

    // ----- Equality — different kinds -----

    [Fact]
    public void DifferentKinds_AlwaysNotEqual() {
        Assert.NotEqual(GrobValue.FromInt(0), GrobValue.FromBool(false));
        Assert.NotEqual(GrobValue.FromInt(0), GrobValue.Nil);
        Assert.NotEqual(GrobValue.FromFloat(1.0), GrobValue.FromInt(1));
        Assert.NotEqual(GrobValue.FromString(""), GrobValue.Nil);
    }

    // ----- Hashing -----

    [Fact]
    public void EqualValues_SameHashCode() {
        Assert.Equal(GrobValue.FromInt(99).GetHashCode(), GrobValue.FromInt(99).GetHashCode());
        Assert.Equal(GrobValue.FromString("a").GetHashCode(), GrobValue.FromString("a").GetHashCode());
        Assert.Equal(GrobValue.Nil.GetHashCode(), GrobValue.Nil.GetHashCode());
    }

    [Fact]
    public void IntAndFloat_SameNumber_DifferentHashCodes() {
        // fromInt(42) and FromFloat(42.0) are different values and should produce different hashes.
        Assert.NotEqual(GrobValue.FromInt(42).GetHashCode(), GrobValue.FromFloat(42.0).GetHashCode());
    }

    // ----- Struct size (layout canary) -----

    [Fact]
    public void GrobValue_SizeIs24Bytes() {
        // Canary: catches accidental field churn that would change the 24-byte layout.
        // Layout: 1-byte discriminator + 7-byte padding + 8-byte scalar + 8-byte reference = 24 bytes.
        Assert.Equal(24, Unsafe.SizeOf<GrobValue>());
    }

    // ----- Equals(object?) overload -----

    [Fact]
    public void EqualsObject_SameValueBoxed_ReturnsTrue() {
        object boxed = GrobValue.FromInt(7);

        Assert.True(GrobValue.FromInt(7).Equals(boxed));
    }

    [Fact]
    public void EqualsObject_Null_ReturnsFalse() {
        Assert.False(GrobValue.FromInt(7).Equals((object?)null));
    }

    [Fact]
    public void EqualsObject_DifferentType_ReturnsFalse() {
        Assert.False(GrobValue.FromInt(7).Equals("not a value"));
    }

    // ----- ToString matrix -----

    [Fact]
    public void ToString_Nil_ReturnsNil() => Assert.Equal("nil", GrobValue.Nil.ToString());

    [Fact]
    public void ToString_BoolTrue_ReturnsTrue() => Assert.Equal("true", GrobValue.FromBool(true).ToString());

    [Fact]
    public void ToString_BoolFalse_ReturnsFalse() => Assert.Equal("false", GrobValue.FromBool(false).ToString());

    [Fact]
    public void ToString_Int_ReturnsDecimal() => Assert.Equal("42", GrobValue.FromInt(42).ToString());

    [Fact]
    public void ToString_Float_ReturnsGFormat() => Assert.Equal("3.14", GrobValue.FromFloat(3.14).ToString());

    [Fact]
    public void ToString_String_ReturnsRawString() => Assert.Equal("hello", GrobValue.FromString("hello").ToString());

    [Fact]
    public void ToString_Array_IncludesCount() {
        var arr = new GrobArray(new[] { GrobValue.FromInt(1), GrobValue.FromInt(2) });

        Assert.Equal("[array(2)]", GrobValue.FromArray(arr).ToString());
    }

    [Fact]
    public void ToString_Map_ReturnsMapMarker() {
        Assert.Equal("[map]", GrobValue.FromMap(new GrobMap()).ToString());
    }

    [Fact]
    public void ToString_Struct_IncludesTypeName() {
        Assert.Equal("[Point]", GrobValue.FromStruct(new GrobStruct("Point")).ToString());
    }

    [Fact]
    public void ToString_Function_IncludesName() {
        Assert.Equal("<fn add>", GrobValue.FromFunction(new BytecodeFunction("add", 2, new Chunk())).ToString());
    }

    // ----- Per-kind GetHashCode and Equals branches -----

    [Fact]
    public void Map_EqualsAndHash_ByReference() {
        var m = new GrobMap();
        var v1 = GrobValue.FromMap(m);
        var v2 = GrobValue.FromMap(m);
        var v3 = GrobValue.FromMap(new GrobMap());

        Assert.Equal(v1, v2);
        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1, v3);
    }

    [Fact]
    public void Function_EqualsAndHash_ByReference() {
        var fn = new BytecodeFunction("f", 0, new Chunk());
        var v1 = GrobValue.FromFunction(fn);
        var v2 = GrobValue.FromFunction(fn);
        var v3 = GrobValue.FromFunction(new BytecodeFunction("f", 0, new Chunk()));

        Assert.Equal(v1, v2);
        Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        Assert.NotEqual(v1, v3);
    }

    [Fact]
    public void Array_GetHashCode_StableAcrossCalls() {
        var arr = new GrobArray();
        var v = GrobValue.FromArray(arr);

        Assert.Equal(v.GetHashCode(), v.GetHashCode());
    }

    [Fact]
    public void Bool_GetHashCode_TrueAndFalseDiffer() {
        Assert.NotEqual(
            GrobValue.FromBool(true).GetHashCode(),
            GrobValue.FromBool(false).GetHashCode());
    }

    [Fact]
    public void Float_GetHashCode_PositiveZeroEqualsNegativeZero() {
        // BitConverter.Int64BitsToDouble(0) and (-0.0) hash differently at the bit level,
        // but double.GetHashCode normalises +0/-0. The struct uses .GetHashCode() so they must match.
        Assert.Equal(
            GrobValue.FromFloat(0.0).GetHashCode(),
            GrobValue.FromFloat(-0.0).GetHashCode());
    }

    [Fact]
    public void Float_OperatorEquality_IntKindFallsBackToEquals() {
        // Mixed-kind via the operator hits the non-Float fast path.
        Assert.True(GrobValue.FromInt(1) == GrobValue.FromInt(1));
        Assert.False(GrobValue.FromInt(1) == GrobValue.FromInt(2));
    }

    // ----- Strict accessor full matrix -----

    [Fact]
    public void StrictAccessor_AllKinds_ReturnInnerValue() {
        Assert.True(GrobValue.FromBool(true).AsBool());
        Assert.Equal(7L, GrobValue.FromInt(7).AsInt());
        Assert.Equal(1.5, GrobValue.FromFloat(1.5).AsFloat());
        Assert.Equal("s", GrobValue.FromString("s").AsString());

        var arr = new GrobArray();
        Assert.Same(arr, GrobValue.FromArray(arr).AsArray());
        var map = new GrobMap();
        Assert.Same(map, GrobValue.FromMap(map).AsMap());
        var st = new GrobStruct("T");
        Assert.Same(st, GrobValue.FromStruct(st).AsStruct());
        var fn = new BytecodeFunction("f", 0, new Chunk());
        Assert.Same(fn, GrobValue.FromFunction(fn).AsFunction());
    }

    [Fact]
    public void TryAccessor_AllKinds_ReturnInnerValue() {
        Assert.True(GrobValue.FromBool(true).TryAsBool(out var b)); Assert.True(b);
        Assert.True(GrobValue.FromInt(3).TryAsInt(out var i)); Assert.Equal(3L, i);
        Assert.True(GrobValue.FromFloat(2.0).TryAsFloat(out var f)); Assert.Equal(2.0, f);
        Assert.True(GrobValue.FromString("x").TryAsString(out var s)); Assert.Equal("x", s);

        var arr = new GrobArray();
        Assert.True(GrobValue.FromArray(arr).TryAsArray(out var a)); Assert.Same(arr, a);
        var map = new GrobMap();
        Assert.True(GrobValue.FromMap(map).TryAsMap(out var m)); Assert.Same(map, m);
        var st = new GrobStruct("T");
        Assert.True(GrobValue.FromStruct(st).TryAsStruct(out var t)); Assert.Same(st, t);
        var fn = new BytecodeFunction("f", 0, new Chunk());
        Assert.True(GrobValue.FromFunction(fn).TryAsFunction(out var k)); Assert.Same(fn, k);
    }

    // ----- Factory null guards -----

    [Fact]
    public void FromString_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => GrobValue.FromString(null!));

    [Fact]
    public void FromArray_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => GrobValue.FromArray(null!));

    [Fact]
    public void FromMap_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => GrobValue.FromMap(null!));

    [Fact]
    public void FromStruct_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => GrobValue.FromStruct(null!));

    [Fact]
    public void FromFunction_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => GrobValue.FromFunction(null!));

    // ----- TryAccessor false paths for reference-type kinds -----

    [Fact]
    public void TryAsArray_WrongKind_ReturnsFalse() {
        Assert.False(GrobValue.FromInt(1).TryAsArray(out _));
    }

    [Fact]
    public void TryAsMap_WrongKind_ReturnsFalse() {
        Assert.False(GrobValue.FromInt(1).TryAsMap(out _));
    }

    [Fact]
    public void TryAsStruct_WrongKind_ReturnsFalse() {
        Assert.False(GrobValue.FromInt(1).TryAsStruct(out _));
    }

    [Fact]
    public void TryAsFunction_WrongKind_ReturnsFalse() {
        Assert.False(GrobValue.FromInt(1).TryAsFunction(out _));
    }

    // ----- GrobArithmeticException constructors -----

    [Fact]
    public void GrobArithmeticException_TwoArgCtor_SetsColumnToZero() {
        // The 3-argument constructor (without column) is the default-column path;
        // verify it correctly delegates with Column = 0.
        var ex = new GrobArithmeticException("E5002", 5, "integer division by zero");

        Assert.Equal("E5002", ex.Code);
        Assert.Equal(5, ex.Line);
        Assert.Equal(0, ex.Column);
        Assert.Equal("integer division by zero", ex.Message);
    }
}
