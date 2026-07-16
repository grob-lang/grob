using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 9 Increment A2 (D-350) — <see cref="OpCode.SetIndex"/>,
/// the array/map index-store companion to <see cref="OpCode.GetIndex"/> (D-348). All chunks
/// are hand-constructed; no compiler dependency. The opcode already existed in the enum
/// (recognised by the disassembler) but had no VM dispatch case until this increment.
/// </summary>
/// <remarks>
/// Array index-write runs end-to-end from source in the integration suite. Map index-write
/// has no source construction path in v1 (no map literal in the parser), so it is hand-built
/// here, mirroring how map <c>for...in</c> is proven in <see cref="VirtualMachineForInTests"/>.
/// </remarks>
public sealed class VirtualMachineIndexAssignmentTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) =>
        (byte)chunk.AddConstant(value);

    // -----------------------------------------------------------------------
    // Array index-write: receiver, index, value, SetIndex — mutates in place.
    // -----------------------------------------------------------------------

    [Fact]
    public void SetIndex_ArrayInRange_MutatesInPlace() {
        var arr = new GrobArray([GrobValue.FromInt(10), GrobValue.FromInt(20), GrobValue.FromInt(30)]);
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(arr)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(1)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(99)), 1);
        chunk.WriteOpCode(OpCode.SetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(99L, arr[1].AsInt());
        Assert.Equal(10L, arr[0].AsInt());
        Assert.Equal(30L, arr[2].AsInt());
    }

    [Fact]
    public void SetIndex_ArrayInRange_LeavesNothingOnTheValueStack() {
        var arr = new GrobArray([GrobValue.FromInt(10)]);
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(arr)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(0)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(1)), 1);
        chunk.WriteOpCode(OpCode.SetIndex, 1);
        // A subsequent Constant push must land at stack depth 0 — proves SetIndex is a
        // pure 3-pop/0-push statement opcode, mirroring SetProperty.
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(7)), 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // SetIndex pops all three of its operands and pushes nothing: the trailing
        // Constant(7) is the only value left on the stack.
        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(7L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Chained target: matrix[r][c] = v — GetIndex reads the inner row, SetIndex writes it.
    // -----------------------------------------------------------------------

    [Fact]
    public void SetIndex_ChainedMatrixTarget_WritesIntoTheCorrectNestedArray() {
        var row0 = new GrobArray([GrobValue.FromInt(1), GrobValue.FromInt(2)]);
        var row1 = new GrobArray([GrobValue.FromInt(3), GrobValue.FromInt(4)]);
        var matrix = new GrobArray([GrobValue.FromArray(row0), GrobValue.FromArray(row1)]);

        var chunk = new Chunk();
        // matrix[1][0] = 9
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(matrix)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(1)), 1);
        chunk.WriteOpCode(OpCode.GetIndex, 1); // → row1
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(0)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(9)), 1);
        chunk.WriteOpCode(OpCode.SetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(9L, row1[0].AsInt());
        Assert.Equal(1L, row0[0].AsInt()); // untouched
    }

    // -----------------------------------------------------------------------
    // Map index-write: no source construction path in v1 (no map literal), hand-built —
    // mirrors VirtualMachineForInTests's map-read precedent.
    // -----------------------------------------------------------------------

    [Fact]
    public void SetIndex_MapExistingKey_OverwritesValue() {
        var map = new GrobMap();
        map.Set("a", GrobValue.FromInt(1));
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("a")), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(42)), 1);
        chunk.WriteOpCode(OpCode.SetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(42L, map["a"].AsInt());
    }

    [Fact]
    public void SetIndex_MapNewKey_InsertsValue() {
        var map = new GrobMap();
        map.Set("a", GrobValue.FromInt(1));
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("b")), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(2)), 1);
        chunk.WriteOpCode(OpCode.SetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(map.TryGetValue("b", out GrobValue value));
        Assert.Equal(2L, value.AsInt());
        Assert.Equal(1L, map["a"].AsInt()); // untouched
    }
}
