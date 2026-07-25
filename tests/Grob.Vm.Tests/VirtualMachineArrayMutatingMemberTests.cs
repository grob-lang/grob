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

    /// <summary>
    /// <c>first.append(appended)</c> through the first reference, discards the nil result,
    /// then loads the second reference and returns it on the stack — the two references
    /// are separate constants in the pool (<c>AddConstant</c> never dedupes), so this
    /// models two distinct Grob-visible bindings over one array, not one CLR alias.
    /// </summary>
    private static Chunk BuildAppendThroughFirstThenLoadSecond(
            GrobValue first, GrobValue second, GrobValue appended) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, first), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("append")), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, appended), 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Pop, 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, second), 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

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
    public void Append_ThroughOneGrobReference_VisibleThroughAnother() {
        // a := [1, 2]; b := a; a.append(3) — under reference semantics (D-372) a and b
        // wrap the SAME GrobArray, so the mutation made through a is observable through b.
        // The two references are modelled as two independent GrobValues — separate
        // constants in the pool — not a CLR-level `alias = shared`, which would be a
        // tautology. The mutation runs through the first constant; the second is loaded
        // by the VM itself and observed on the stack.
        var shared = new GrobArray(Ints(1, 2));
        GrobValue referenceA = GrobValue.FromArray(shared);
        GrobValue referenceB = GrobValue.FromArray(shared);

        var (vm, _) = NewVm();
        vm.Run(BuildAppendThroughFirstThenLoadSecond(referenceA, referenceB, GrobValue.FromInt(3)));

        GrobValue observedThroughB = vm.Stack.Peek();
        Assert.True(observedThroughB.TryAsArray(out GrobArray? throughB));
        Assert.Equal(3, throughB!.Count);
        Assert.Equal(GrobValue.FromInt(3), throughB[2]);

        // Both references denote the one underlying instance — reference, not value.
        Assert.True(referenceA.TryAsArray(out GrobArray? throughA));
        Assert.Same(throughA, throughB);
    }
}
