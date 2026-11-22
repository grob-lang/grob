using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-448 Decision 7: <see cref="FunctionTypeDescriptor.Merge"/> combines two compatible
/// function shapes. An <see cref="GrobType.Unknown"/> parameter, as an inferred lambda
/// carries, takes the other shape's type; an Unknown return stays Unknown, with the other
/// shape's concrete return kept as its bound; two concrete positions must agree, and so
/// must arity.
/// </summary>
public sealed class FunctionTypeDescriptorMergeTests {
    private static FunctionTypeDescriptor Fn(GrobType returnType, params GrobType[] parameters) =>
        new(parameters, returnType);

    [Fact]
    public void Merge_UnknownParameter_TakesTheConcreteType() {
        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(
            Fn(GrobType.Int, GrobType.Unknown),
            Fn(GrobType.Int, GrobType.Int));

        Assert.Equal(Fn(GrobType.Int, GrobType.Int), merged);
    }

    /// <summary>
    /// An inferred lambda's Unknown return is not shown to be the other shape's type (its
    /// body may return anything, such as its own argument), so the merged return stays
    /// Unknown and no typed opcode is chosen from it. The concrete return is kept as the
    /// return's bound, so an annotation that conflicts with it is still refused.
    /// </summary>
    [Fact]
    public void Merge_UnknownReturn_StaysUnknownWithTheConcreteReturnAsItsBound() {
        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(
            Fn(GrobType.String, GrobType.Int),
            Fn(GrobType.Unknown, GrobType.Unknown));

        Assert.NotNull(merged);
        Assert.Equal(GrobType.Unknown, merged.ReturnType);
        Assert.Equal(GrobType.String, merged.ReturnBound);
        Assert.Null(merged.ReturnBoundDescriptor);
        Assert.Equal([GrobType.Int], merged.ParameterTypes);
    }

    [Fact]
    public void Merge_TwoUnknownReturns_HaveNoBound() {
        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(
            Fn(GrobType.Unknown, GrobType.Int),
            Fn(GrobType.Unknown, GrobType.Unknown));

        Assert.Equal(Fn(GrobType.Unknown, GrobType.Int), merged);
        Assert.Null(merged!.ReturnBound);
    }

    [Fact]
    public void Merge_UnknownReturnWithNestedShape_KeepsTheNestedShapeAsTheBound() {
        FunctionTypeDescriptor tracked = new([], GrobType.Function, [], Fn(GrobType.Int));
        FunctionTypeDescriptor inferred = new([], GrobType.Unknown, [], null);

        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(inferred, tracked);

        Assert.NotNull(merged);
        Assert.Equal(GrobType.Unknown, merged.ReturnType);
        Assert.Null(merged.ReturnDescriptor);
        Assert.Equal(GrobType.Function, merged.ReturnBound);
        Assert.Equal(Fn(GrobType.Int), merged.ReturnBoundDescriptor);
    }

    /// <summary>
    /// Combining through an inferred shape does not lose a conflict: the bound left by the
    /// first two shapes conflicts with the third's concrete return, as the first and third
    /// would conflict if combined directly.
    /// </summary>
    [Fact]
    public void Merge_BoundConflictingWithALaterConcreteReturn_IsNull() {
        FunctionTypeDescriptor? bounded = FunctionTypeDescriptor.Merge(
            Fn(GrobType.Int, GrobType.Int),
            Fn(GrobType.Unknown, GrobType.Unknown));

        Assert.NotNull(bounded);
        Assert.Null(FunctionTypeDescriptor.Merge(bounded, Fn(GrobType.String, GrobType.Int)));
        Assert.Null(FunctionTypeDescriptor.Merge(Fn(GrobType.String, GrobType.Int), bounded));
    }

    [Fact]
    public void Merge_TwoBoundsThatAgree_KeepTheBound() {
        FunctionTypeDescriptor? left = FunctionTypeDescriptor.Merge(
            Fn(GrobType.Int, GrobType.Int), Fn(GrobType.Unknown, GrobType.Unknown));
        FunctionTypeDescriptor? right = FunctionTypeDescriptor.Merge(
            Fn(GrobType.Unknown, GrobType.Unknown), Fn(GrobType.Int, GrobType.Int));

        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(left!, right!);

        Assert.NotNull(merged);
        Assert.Equal(GrobType.Unknown, merged.ReturnType);
        Assert.Equal(GrobType.Int, merged.ReturnBound);
    }

    [Fact]
    public void Merge_TwoBoundsThatConflict_IsNull() {
        FunctionTypeDescriptor? left = FunctionTypeDescriptor.Merge(
            Fn(GrobType.Int, GrobType.Int), Fn(GrobType.Unknown, GrobType.Unknown));
        FunctionTypeDescriptor? right = FunctionTypeDescriptor.Merge(
            Fn(GrobType.String, GrobType.Int), Fn(GrobType.Unknown, GrobType.Unknown));

        Assert.Null(FunctionTypeDescriptor.Merge(left!, right!));
    }

    [Fact]
    public void Equals_DescriptorsThatDifferOnlyInBound_AreNotEqual() {
        FunctionTypeDescriptor? bounded = FunctionTypeDescriptor.Merge(
            Fn(GrobType.Int, GrobType.Int), Fn(GrobType.Unknown, GrobType.Unknown));
        FunctionTypeDescriptor unbounded = Fn(GrobType.Unknown, GrobType.Int);

        Assert.NotEqual(unbounded, bounded);
        Assert.NotEqual(bounded, unbounded);
    }

    [Fact]
    public void Merge_ConflictingConcreteReturn_IsNull() {
        Assert.Null(FunctionTypeDescriptor.Merge(Fn(GrobType.Int, GrobType.Int), Fn(GrobType.String, GrobType.Int)));
    }

    [Fact]
    public void Merge_ConflictingConcreteParameter_IsNull() {
        Assert.Null(FunctionTypeDescriptor.Merge(Fn(GrobType.Int, GrobType.Int), Fn(GrobType.Int, GrobType.Float)));
    }

    [Fact]
    public void Merge_DifferentArity_IsNull() {
        Assert.Null(FunctionTypeDescriptor.Merge(Fn(GrobType.Int, GrobType.Int), Fn(GrobType.Int)));
    }

    [Fact]
    public void Merge_NestedReturnShapes_MergeRecursively() {
        FunctionTypeDescriptor left = new([], GrobType.Function, [], Fn(GrobType.Int, GrobType.Unknown));
        FunctionTypeDescriptor right = new([], GrobType.Function, [], Fn(GrobType.Unknown, GrobType.Int));

        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(left, right);

        Assert.NotNull(merged);
        Assert.Equal(GrobType.Function, merged.ReturnType);
        FunctionTypeDescriptor nested = Assert.IsType<FunctionTypeDescriptor>(merged.ReturnDescriptor);
        Assert.Equal([GrobType.Int], nested.ParameterTypes);
        Assert.Equal(GrobType.Unknown, nested.ReturnType);
        Assert.Equal(GrobType.Int, nested.ReturnBound);
    }

    [Fact]
    public void Merge_NestedParameterShapes_MergeRecursively() {
        FunctionTypeDescriptor left = new([GrobType.Function], GrobType.Int, [Fn(GrobType.Unknown, GrobType.Int)], null);
        FunctionTypeDescriptor right = new([GrobType.Function], GrobType.Int, [Fn(GrobType.Int, GrobType.Unknown)], null);

        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(left, right);

        Assert.NotNull(merged);
        Assert.Equal([GrobType.Function], merged.ParameterTypes);
        Assert.Equal(GrobType.Int, merged.ReturnType);
        FunctionTypeDescriptor nested = Assert.IsType<FunctionTypeDescriptor>(merged.ParameterDescriptorAt(0));
        Assert.Equal([GrobType.Int], nested.ParameterTypes);
        Assert.Equal(GrobType.Unknown, nested.ReturnType);
        Assert.Equal(GrobType.Int, nested.ReturnBound);
    }

    [Fact]
    public void Merge_NestedShapeTrackedOnOneSideOnly_IsNull() {
        FunctionTypeDescriptor tracked = new([], GrobType.Function, [], Fn(GrobType.Int));
        FunctionTypeDescriptor untracked = new([], GrobType.Function, [], null);

        Assert.Null(FunctionTypeDescriptor.Merge(tracked, untracked));
    }

    [Fact]
    public void Merge_NestedShapesConflict_IsNull() {
        FunctionTypeDescriptor left = new([], GrobType.Function, [], Fn(GrobType.Int));
        FunctionTypeDescriptor right = new([], GrobType.Function, [], Fn(GrobType.String));

        Assert.Null(FunctionTypeDescriptor.Merge(left, right));
    }
}
