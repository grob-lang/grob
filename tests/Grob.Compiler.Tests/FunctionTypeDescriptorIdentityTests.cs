using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-463: a function type's struct, array and map positions carry a
/// <see cref="PositionIdentity"/>, which takes part in the descriptor's equality and hash
/// and in <see cref="FunctionTypeDescriptor.Merge"/>; <see cref="ArrayTypeDescriptor"/> and
/// <see cref="MapTypeDescriptor"/> compare by value, function element and value included.
/// </summary>
public sealed class FunctionTypeDescriptorIdentityTests {
    private static readonly PositionIdentity IntArray = new(ArrayDescriptor: new ArrayTypeDescriptor(GrobType.Int));
    private static readonly PositionIdentity FloatArray = new(ArrayDescriptor: new ArrayTypeDescriptor(GrobType.Float));

    private static FunctionTypeDescriptor Fn(GrobType returnType, params GrobType[] parameters) =>
        new(parameters, returnType);

    private static FunctionTypeDescriptor ArrayParameter(PositionIdentity? identity) =>
        new([GrobType.Array], GrobType.Int) { ParameterIdentities = [identity] };

    private static FunctionTypeDescriptor ArrayReturn(PositionIdentity? identity) =>
        new([], GrobType.Array) { ReturnIdentity = identity };

    // -----------------------------------------------------------------------
    // PositionIdentity
    // -----------------------------------------------------------------------

    [Fact]
    public void Of_NoChannel_IsNull() =>
        Assert.Null(PositionIdentity.Of(null, null, null));

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Of_AnyChannel_CarriesIt(bool named, bool array, bool map) {
        ArrayTypeDescriptor? arrayDescriptor = array ? new ArrayTypeDescriptor(GrobType.Int) : null;
        MapTypeDescriptor? mapDescriptor = map ? new MapTypeDescriptor(GrobType.Int) : null;

        PositionIdentity? identity = PositionIdentity.Of(named ? "A" : null, arrayDescriptor, mapDescriptor);

        Assert.Equal(new PositionIdentity(named ? "A" : null, arrayDescriptor, mapDescriptor), identity);
    }

    // -----------------------------------------------------------------------
    // Array and map descriptors compare by value
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayDescriptor_SameChannels_AreEqualWithEqualHashes() {
        ArrayTypeDescriptor left = new(GrobType.Function, elementFunctionDescriptor: Fn(GrobType.Int, GrobType.Int));
        ArrayTypeDescriptor right = new(GrobType.Function, elementFunctionDescriptor: Fn(GrobType.Int, GrobType.Int));

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ArrayDescriptor_DifferentElementFunction_AreNotEqual() =>
        Assert.NotEqual(
            new ArrayTypeDescriptor(GrobType.Function, elementFunctionDescriptor: Fn(GrobType.Int, GrobType.Int)),
            new ArrayTypeDescriptor(GrobType.Function, elementFunctionDescriptor: Fn(GrobType.Float, GrobType.Float)));

    [Fact]
    public void MapDescriptor_DifferentValueFunction_AreNotEqual() =>
        Assert.NotEqual(
            new MapTypeDescriptor(GrobType.Function, valueFunctionDescriptor: Fn(GrobType.Int, GrobType.Int)),
            new MapTypeDescriptor(GrobType.Function, valueFunctionDescriptor: Fn(GrobType.Float, GrobType.Float)));

    [Fact]
    public void MapDescriptor_SameChannels_AreEqual() =>
        Assert.Equal(
            new MapTypeDescriptor(GrobType.Function, valueFunctionDescriptor: Fn(GrobType.Int, GrobType.Int)),
            new MapTypeDescriptor(GrobType.Function, valueFunctionDescriptor: Fn(GrobType.Int, GrobType.Int)));

    // -----------------------------------------------------------------------
    // Equality and hash of a function descriptor
    // -----------------------------------------------------------------------

    [Fact]
    public void Equals_DifferentParameterIdentities_AreNotEqual() =>
        Assert.NotEqual(ArrayParameter(IntArray), ArrayParameter(FloatArray));

    [Fact]
    public void Equals_DifferentReturnIdentities_AreNotEqual() =>
        Assert.NotEqual(ArrayReturn(IntArray), ArrayReturn(FloatArray));

    [Fact]
    public void Equals_SameIdentities_AreEqualWithEqualHashes() {
        FunctionTypeDescriptor left = ArrayParameter(IntArray);
        FunctionTypeDescriptor right = ArrayParameter(new PositionIdentity(ArrayDescriptor: new ArrayTypeDescriptor(GrobType.Int)));

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    /// <summary>
    /// A shape with no identities equals one whose identities are all null, as an empty
    /// nested-descriptor list equals an all-null one.
    /// </summary>
    [Fact]
    public void Equals_EmptyIdentitiesAndAllNull_AreEqualWithEqualHashes() {
        FunctionTypeDescriptor empty = Fn(GrobType.Int, GrobType.Int, GrobType.Int);
        FunctionTypeDescriptor allNull = new([GrobType.Int, GrobType.Int], GrobType.Int) { ParameterIdentities = [null, null] };

        Assert.Equal(empty, allNull);
        Assert.Equal(empty.GetHashCode(), allNull.GetHashCode());
    }

    [Fact]
    public void ParameterIdentityAt_PastTheList_IsNull() =>
        Assert.Null(Fn(GrobType.Int, GrobType.Array).ParameterIdentityAt(0));

    // -----------------------------------------------------------------------
    // Merge
    // -----------------------------------------------------------------------

    [Fact]
    public void Merge_SameParameterIdentity_KeepsIt() {
        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(ArrayParameter(IntArray), ArrayParameter(IntArray));

        Assert.NotNull(merged);
        Assert.Equal(IntArray, merged.ParameterIdentityAt(0));
    }

    [Fact]
    public void Merge_DifferentParameterIdentities_Conflict() =>
        Assert.Null(FunctionTypeDescriptor.Merge(ArrayParameter(IntArray), ArrayParameter(FloatArray)));

    [Fact]
    public void Merge_OneParameterIdentityMissing_TakesTheOther() {
        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(ArrayParameter(null), ArrayParameter(FloatArray));

        Assert.NotNull(merged);
        Assert.Equal(FloatArray, merged.ParameterIdentityAt(0));
    }

    [Fact]
    public void Merge_UnknownParameter_TakesTheOtherIdentity() {
        FunctionTypeDescriptor inferred = Fn(GrobType.Int, GrobType.Unknown);

        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(inferred, ArrayParameter(IntArray));

        Assert.NotNull(merged);
        Assert.Equal(GrobType.Array, merged.ParameterTypes[0]);
        Assert.Equal(IntArray, merged.ParameterIdentityAt(0));
    }

    [Fact]
    public void Merge_DifferentReturnIdentities_Conflict() =>
        Assert.Null(FunctionTypeDescriptor.Merge(ArrayReturn(IntArray), ArrayReturn(FloatArray)));

    [Fact]
    public void Merge_SameReturnIdentity_KeepsIt() {
        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(ArrayReturn(IntArray), ArrayReturn(IntArray));

        Assert.NotNull(merged);
        Assert.Equal(IntArray, merged.ReturnIdentity);
    }

    /// <summary>
    /// An inferred lambda's Unknown return keeps the other shape's return as its bound, and
    /// the bound keeps that return's identity, so an annotation of another array type is
    /// still refused through it.
    /// </summary>
    [Fact]
    public void Merge_UnknownReturn_KeepsTheConcreteReturnIdentityOnTheBound() {
        FunctionTypeDescriptor inferred = new([], GrobType.Unknown);

        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(inferred, ArrayReturn(IntArray));

        Assert.NotNull(merged);
        Assert.Equal(GrobType.Array, merged.ReturnBound);
        Assert.Equal(IntArray, merged.ReturnBoundIdentity);
        Assert.Null(merged.ReturnIdentity);
    }

    [Fact]
    public void Merge_TwoBoundsWithDifferentIdentities_Conflict() {
        FunctionTypeDescriptor? left = FunctionTypeDescriptor.Merge(new([], GrobType.Unknown), ArrayReturn(IntArray));
        FunctionTypeDescriptor? right = FunctionTypeDescriptor.Merge(new([], GrobType.Unknown), ArrayReturn(FloatArray));

        Assert.Null(FunctionTypeDescriptor.Merge(left!, right!));
    }
}
