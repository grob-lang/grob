using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

/// <summary>
/// Coverage tests for the runtime collection / reference types:
/// <see cref="GrobArray"/>, <see cref="GrobMap"/>, <see cref="GrobStruct"/>,
/// <see cref="GrobFunction"/>.
///
/// The round-trip tests in <see cref="GrobValueTests"/> only exercise the
/// constructor + identity. This file exercises the full public surface.
/// </summary>
public sealed class RuntimeTypesTests {
    // ----- GrobArray -----

    [Fact]
    public void GrobArray_DefaultConstructor_IsEmpty() {
        var arr = new GrobArray();

        Assert.Equal(0, arr.Count);
        Assert.Empty(arr.Elements);
    }

    [Fact]
    public void GrobArray_ConstructorWithElements_CopiesThem() {
        var arr = new GrobArray(new[] { GrobValue.FromInt(1), GrobValue.FromInt(2) });

        Assert.Equal(2, arr.Count);
        Assert.Equal(GrobValue.FromInt(1), arr.Elements[0]);
        Assert.Equal(GrobValue.FromInt(2), arr.Elements[1]);
    }

    [Fact]
    public void GrobArray_Add_AppendsAndIncrementsCount() {
        var arr = new GrobArray();
        arr.Add(GrobValue.FromString("a"));
        arr.Add(GrobValue.FromString("b"));

        Assert.Equal(2, arr.Count);
        Assert.Equal(GrobValue.FromString("a"), arr[0]);
        Assert.Equal(GrobValue.FromString("b"), arr[1]);
    }

    [Fact]
    public void GrobArray_Indexer_GetAndSet_ReturnsAndUpdatesElement() {
        var arr = new GrobArray(new[] { GrobValue.FromInt(10), GrobValue.FromInt(20) });

        Assert.Equal(GrobValue.FromInt(10), arr[0]);
        arr[0] = GrobValue.FromInt(99);
        Assert.Equal(GrobValue.FromInt(99), arr[0]);
    }

    // ----- GrobMap -----

    [Fact]
    public void GrobMap_DefaultConstructor_IsEmpty() {
        var map = new GrobMap();

        Assert.Empty(map.Entries);
    }

    [Fact]
    public void GrobMap_Set_AddsEntry_ThenIndexerReadsIt() {
        var map = new GrobMap();
        map.Set("key", GrobValue.FromInt(42));

        Assert.Equal(GrobValue.FromInt(42), map["key"]);
        Assert.Single(map.Entries);
    }

    [Fact]
    public void GrobMap_Indexer_Set_OverwritesExistingEntry() {
        var map = new GrobMap();
        map["k"] = GrobValue.FromInt(1);
        map["k"] = GrobValue.FromInt(2);

        Assert.Equal(GrobValue.FromInt(2), map["k"]);
    }

    [Fact]
    public void GrobMap_TryGetValue_Hit_ReturnsTrueAndValue() {
        var map = new GrobMap();
        map.Set("found", GrobValue.FromString("yes"));

        Assert.True(map.TryGetValue("found", out var value));
        Assert.Equal(GrobValue.FromString("yes"), value);
    }

    [Fact]
    public void GrobMap_TryGetValue_Miss_ReturnsFalseAndDefault() {
        var map = new GrobMap();

        Assert.False(map.TryGetValue("nope", out var value));
        Assert.Equal(GrobValue.Nil, value);
    }

    // ----- GrobStruct -----

    [Fact]
    public void GrobStruct_DefaultConstructor_HasNoFields() {
        var s = new GrobStruct("Empty");

        Assert.Equal("Empty", s.TypeName);
        Assert.False(s.TryGetField("anything", out _));
    }

    [Fact]
    public void GrobStruct_SetField_ThenGetField_RoundTrips() {
        var s = new GrobStruct("Point");
        s.SetField("x", GrobValue.FromInt(3));

        Assert.Equal(GrobValue.FromInt(3), s.GetField("x"));
    }

    [Fact]
    public void GrobStruct_TryGetField_Hit_ReturnsTrueAndValue() {
        var s = new GrobStruct("Box");
        s.SetField("v", GrobValue.FromString("hi"));

        Assert.True(s.TryGetField("v", out var v));
        Assert.Equal(GrobValue.FromString("hi"), v);
    }

    [Fact]
    public void GrobStruct_Equals_NullOther_ReturnsFalse() {
        var s = new GrobStruct("T");

        Assert.False(s.Equals(null));
        Assert.False(s.Equals((object?)null));
    }

    [Fact]
    public void GrobStruct_Equals_DifferentTypeName_ReturnsFalse() {
        var a = new GrobStruct("A");
        var b = new GrobStruct("B");

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void GrobStruct_Equals_DifferentFieldCount_ReturnsFalse() {
        var a = new GrobStruct("T");
        a.SetField("x", GrobValue.FromInt(1));
        var b = new GrobStruct("T");
        b.SetField("x", GrobValue.FromInt(1));
        b.SetField("y", GrobValue.FromInt(2));

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void GrobStruct_Equals_MissingFieldInOther_ReturnsFalse() {
        var a = new GrobStruct("T");
        a.SetField("x", GrobValue.FromInt(1));
        var b = new GrobStruct("T");
        b.SetField("y", GrobValue.FromInt(1));   // same count, different key

        Assert.False(a.Equals(b));
    }

    [Fact]
    public void GrobStruct_Equals_NonGrobStructObject_ReturnsFalse() {
        var s = new GrobStruct("T");
        object other = "not a struct";

        Assert.False(s.Equals(other));
    }

    [Fact]
    public void GrobStruct_ConstructorWithFields_CopiesThem() {
        var s = new GrobStruct("Point", new[] {
            new KeyValuePair<string, GrobValue>("x", GrobValue.FromInt(1)),
            new KeyValuePair<string, GrobValue>("y", GrobValue.FromInt(2)),
        });

        Assert.Equal(GrobValue.FromInt(1), s.GetField("x"));
        Assert.Equal(GrobValue.FromInt(2), s.GetField("y"));
    }

    [Fact]
    public void GrobStruct_HashCode_OrderIndependent() {
        var a = new GrobStruct("T");
        a.SetField("x", GrobValue.FromInt(1));
        a.SetField("y", GrobValue.FromInt(2));
        var b = new GrobStruct("T");
        b.SetField("y", GrobValue.FromInt(2));   // insertion order swapped
        b.SetField("x", GrobValue.FromInt(1));

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a.Equals(b));
    }

    // ----- GrobFunction (via its concrete BytecodeFunction subclass) -----

    [Fact]
    public void GrobFunction_Constructor_StoresNameAndArity() {
        var fn = new BytecodeFunction("add", 2, new Chunk());

        Assert.Equal("add", fn.Name);
        Assert.Equal(2, fn.Arity);
    }

    [Fact]
    public void GrobFunction_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new BytecodeFunction(null!, 0, new Chunk()));

    [Fact]
    public void GrobFunction_NegativeArity_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new BytecodeFunction("f", -1, new Chunk()));

    [Fact]
    public void BytecodeFunction_NullBytecode_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new BytecodeFunction("f", 0, null!));

    [Fact]
    public void BytecodeFunction_NegativeUpvalueCount_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new BytecodeFunction("f", 0, new Chunk(), upvalueCount: -1));

    [Fact]
    public void BytecodeFunction_StoresUpvalueCount() {
        var fn = new BytecodeFunction("f", 0, new Chunk(), upvalueCount: 3);
        Assert.Equal(3, fn.UpvalueCount);
    }

    [Fact]
    public void BytecodeFunction_StoresBytecodeChunk() {
        var chunk = new Chunk();
        var fn = new BytecodeFunction("f", 0, chunk);
        Assert.Same(chunk, fn.Bytecode);
    }

    [Fact]
    public void GrobFunction_EmptyName_IsAllowed_ForAnonymousLambdas() {
        var fn = new BytecodeFunction("", 0, new Chunk());
        Assert.Equal("", fn.Name);
    }

    // ----- GrobStruct boundary guards -----

    [Fact]
    public void GrobStruct_NullTypeName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new GrobStruct(null!));

    [Fact]
    public void GrobStruct_EmptyTypeName_Throws() =>
        Assert.Throws<ArgumentException>(() => new GrobStruct(""));

    [Fact]
    public void GrobStruct_GetField_NullName_Throws() {
        var s = new GrobStruct("T");
        Assert.Throws<ArgumentNullException>(() => s.GetField(null!));
    }

    [Fact]
    public void GrobStruct_SetField_NullName_Throws() {
        var s = new GrobStruct("T");
        Assert.Throws<ArgumentNullException>(() => s.SetField(null!, GrobValue.Nil));
    }

    [Fact]
    public void GrobStruct_TryGetField_NullName_Throws() {
        var s = new GrobStruct("T");
        Assert.Throws<ArgumentNullException>(() => s.TryGetField(null!, out _));
    }

    [Fact]
    public void GrobStruct_GetField_MissingKey_Throws() {
        var s = new GrobStruct("T");
        Assert.Throws<KeyNotFoundException>(() => s.GetField("nope"));
    }

    [Fact]
    public void GrobStruct_TryGetField_Miss_ReturnsFalse() {
        var s = new GrobStruct("T");
        Assert.False(s.TryGetField("nope", out _));
    }

    // ----- NativeFunction -----

    [Fact]
    public void NativeFunction_Constructor_StoresNameArityAndImpl() {
        Func<GrobValue[], VmInvoker, GrobValue> impl = static (_, _) => GrobValue.Nil;
        var fn = new NativeFunction("double", 1, impl);

        Assert.Equal("double", fn.Name);
        Assert.Equal(1, fn.Arity);
        Assert.Same(impl, fn.Implementation);
    }

    [Fact]
    public void NativeFunction_WrapsIntoFunctionValue() {
        var fn = new NativeFunction("add", 2, static (_, _) => GrobValue.Nil);
        var value = GrobValue.FromFunction(fn);

        Assert.True(value.TryAsFunction(out GrobFunction? got));
        Assert.Same(fn, got);
    }

    [Fact]
    public void NativeFunction_ToString_IncludesName() {
        var fn = new NativeFunction("triple", 1, static (_, _) => GrobValue.Nil);
        var value = GrobValue.FromFunction(fn);

        Assert.Contains("triple", value.ToString());
    }

    [Fact]
    public void NativeFunction_NullImplementation_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new NativeFunction("f", 0, null!));

    [Fact]
    public void NativeFunction_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            new NativeFunction(null!, 0, static (_, _) => GrobValue.Nil));

    [Fact]
    public void NativeFunction_NegativeArity_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NativeFunction("f", -1, static (_, _) => GrobValue.Nil));

    [Fact]
    public void GrobStruct_Equals_SameInstance_ReturnsTrue() {
        var s = new GrobStruct("T");
        s.SetField("x", GrobValue.FromInt(1));
        Assert.True(s.Equals(s));
    }

    [Fact]
    public void GrobStruct_Equals_SameTypeAndFields_ReturnsTrue() {
        var a = new GrobStruct("T");
        a.SetField("x", GrobValue.FromInt(1));
        a.SetField("y", GrobValue.FromInt(2));
        var b = new GrobStruct("T");
        b.SetField("x", GrobValue.FromInt(1));
        b.SetField("y", GrobValue.FromInt(2));

        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void GrobStruct_Equals_DifferentFieldValue_ReturnsFalse() {
        var a = new GrobStruct("T");
        a.SetField("x", GrobValue.FromInt(1));
        var b = new GrobStruct("T");
        b.SetField("x", GrobValue.FromInt(2));

        Assert.False(a.Equals(b));
    }
}
