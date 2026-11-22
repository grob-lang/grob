using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-448 Decision 7: <see cref="FunctionTypeDescriptor.Merge"/> combines two compatible
/// function shapes. An <see cref="GrobType.Unknown"/> parameter, as an inferred lambda
/// carries, takes the other shape's type; an Unknown return stays Unknown; two concrete
/// positions must agree, and so must arity.
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
    /// Unknown and no typed opcode is chosen from it.
    /// </summary>
    [Fact]
    public void Merge_UnknownReturn_StaysUnknown() {
        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(
            Fn(GrobType.String, GrobType.Int),
            Fn(GrobType.Unknown, GrobType.Unknown));

        Assert.Equal(Fn(GrobType.Unknown, GrobType.Int), merged);
    }

    [Fact]
    public void Merge_UnknownReturnWithNestedShape_DropsTheNestedShape() {
        FunctionTypeDescriptor tracked = new([], GrobType.Function, [], Fn(GrobType.Int));
        FunctionTypeDescriptor inferred = new([], GrobType.Unknown, [], null);

        Assert.Equal(new FunctionTypeDescriptor([], GrobType.Unknown), FunctionTypeDescriptor.Merge(inferred, tracked));
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

        Assert.Equal(new FunctionTypeDescriptor([], GrobType.Function, [], Fn(GrobType.Unknown, GrobType.Int)), merged);
    }

    [Fact]
    public void Merge_NestedParameterShapes_MergeRecursively() {
        FunctionTypeDescriptor left = new([GrobType.Function], GrobType.Int, [Fn(GrobType.Unknown, GrobType.Int)], null);
        FunctionTypeDescriptor right = new([GrobType.Function], GrobType.Int, [Fn(GrobType.Int, GrobType.Unknown)], null);

        FunctionTypeDescriptor? merged = FunctionTypeDescriptor.Merge(left, right);

        Assert.Equal(new FunctionTypeDescriptor([GrobType.Function], GrobType.Int, [Fn(GrobType.Unknown, GrobType.Int)], null), merged);
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
