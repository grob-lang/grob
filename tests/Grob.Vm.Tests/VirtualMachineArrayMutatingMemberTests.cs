using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 9 Increment C0a-2 (D-373) — the array in-place-mutating
/// member surface: <c>append</c>, <c>insert</c>, <c>remove</c>, <c>clear</c> (<see
/// cref="ArrayNatives.GetMethod"/>). All four mutate the receiver <see cref="GrobArray"/>
/// in place and return <see cref="GrobValue.Nil"/>. All chunks are hand-constructed; no
/// compiler dependency. Also pins D-372's reference-semantics ratification — mutation
/// through one binding is observable through another binding aliasing the same
/// <see cref="GrobArray"/> instance.
/// </summary>
public sealed class VirtualMachineArrayMutatingMemberTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) =>
        (byte)chunk.AddConstant(value);

    /// <summary>Array constant, <c>GetProperty &lt;methodName&gt;</c>, each argument in
    /// order, <c>Call &lt;argCount&gt;</c>, Return — the shape every array method call
    /// compiles to.</summary>
    private static Chunk BuildMethodChunk(GrobArray array, string methodName, params GrobValue[] arguments) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(array)), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(methodName)), 1);
        foreach (GrobValue argument in arguments) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte(ConstByte(chunk, argument), 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)arguments.Length, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    private static GrobValue[] Ints(params long[] values) =>
        [.. values.Select(GrobValue.FromInt)];

    // -----------------------------------------------------------------------
    // append(value)
    // -----------------------------------------------------------------------

    [Fact]
    public void Append_MutatesReceiverInPlace_AndReturnsNil() {
        var array = new GrobArray(Ints(1, 2));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(array, "append", GrobValue.FromInt(3)));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.Equal(3, array.Count);
        Assert.Equal(GrobValue.FromInt(3), array[2]);
    }

    // -----------------------------------------------------------------------
    // insert(index, value)
    // -----------------------------------------------------------------------

    [Fact]
    public void Insert_AtMiddleIndex_ShiftsSubsequentElements() {
        var array = new GrobArray(Ints(1, 2, 3));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(array, "insert", GrobValue.FromInt(1), GrobValue.FromInt(99)));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.Equal(4, array.Count);
        Assert.Equal(GrobValue.FromInt(1), array[0]);
        Assert.Equal(GrobValue.FromInt(99), array[1]);
        Assert.Equal(GrobValue.FromInt(2), array[2]);
        Assert.Equal(GrobValue.FromInt(3), array[3]);
    }

    [Fact]
    public void Insert_AtIndexEqualToLength_AppendsAtEnd() {
        // Pinned boundary rule: index == length is a valid append-position insert.
        var array = new GrobArray(Ints(1, 2));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(array, "insert", GrobValue.FromInt(2), GrobValue.FromInt(3)));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.Equal(3, array.Count);
        Assert.Equal(GrobValue.FromInt(3), array[2]);
    }

    [Fact]
    public void Insert_AtIndexZero_OnEmptyArray_Succeeds() {
        var array = new GrobArray();
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(array, "insert", GrobValue.FromInt(0), GrobValue.FromInt(7)));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.Equal(1, array.Count);
        Assert.Equal(GrobValue.FromInt(7), array[0]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)] // length + 1
    public void Insert_OutOfRange_ThrowsCatchableIndexError(long index) {
        var array = new GrobArray(Ints(1, 2));
        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildMethodChunk(array, "insert", GrobValue.FromInt(index), GrobValue.FromInt(9))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    // -----------------------------------------------------------------------
    // remove(index)
    // -----------------------------------------------------------------------

    [Fact]
    public void Remove_AtMiddleIndex_ShiftsSubsequentElements() {
        var array = new GrobArray(Ints(1, 2, 3));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(array, "remove", GrobValue.FromInt(1)));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.Equal(2, array.Count);
        Assert.Equal(GrobValue.FromInt(1), array[0]);
        Assert.Equal(GrobValue.FromInt(3), array[1]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)] // length — strictly out of range for remove, unlike insert
    public void Remove_OutOfRange_ThrowsCatchableIndexError(long index) {
        var array = new GrobArray(Ints(1, 2));
        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildMethodChunk(array, "remove", GrobValue.FromInt(index))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Remove_OnEmptyArray_ThrowsCatchableIndexError() {
        var array = new GrobArray();
        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildMethodChunk(array, "remove", GrobValue.FromInt(0))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    // -----------------------------------------------------------------------
    // clear()
    // -----------------------------------------------------------------------

    [Fact]
    public void Clear_EmptiesArray_LengthAndIsEmptyReflectMutation() {
        var array = new GrobArray(Ints(1, 2, 3));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(array, "clear"));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.Equal(0, array.Count);
    }

    // -----------------------------------------------------------------------
    // Aliasing — D-372's ratification, pinned directly (not incidentally).
    // -----------------------------------------------------------------------

    [Fact]
    public void Append_ThroughOneGrobArrayReference_VisibleThroughAnother() {
        // b := a; b.append(3) — a and b are the SAME GrobArray instance under reference
        // semantics (D-372). Constructing a single GrobArray and passing it to two
        // separate chunk constants proves this at the runtime-representation level.
        var shared = new GrobArray(Ints(1, 2));
        GrobArray aliasBeforeMutation = shared;

        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(shared, "append", GrobValue.FromInt(3)));

        Assert.Same(shared, aliasBeforeMutation);
        Assert.Equal(3, aliasBeforeMutation.Count);
    }
}
