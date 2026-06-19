using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 4 Increment C — the runtime operations the
/// <c>for...in</c> lowering relies on: <see cref="OpCode.NewArray"/>,
/// <see cref="OpCode.GetIndex"/> (array by int, map by string key) and
/// <see cref="OpCode.GetProperty"/> (<c>length</c> on an array, <c>keys</c> on a
/// map). All chunks are hand-constructed; no compiler dependency.
/// </summary>
/// <remarks>
/// Array and numeric-range iteration run end-to-end from source in the
/// integration suite. Map iteration has no source construction path in v1 (there
/// is no map literal in the parser — out-of-scope parser work), so a full
/// <c>for k, v in m</c> lowering is hand-built here to prove insertion-order
/// iteration and that <c>v</c> matches <c>m[k]</c> at runtime.
/// </remarks>
public sealed class VirtualMachineForInTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) =>
        (byte)chunk.AddConstant(value);

    // -----------------------------------------------------------------------
    // NewArray
    // -----------------------------------------------------------------------

    [Fact]
    public void NewArray_BuildsArrayFromStackValuesInOrder() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(10)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(20)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(30)), 1);
        chunk.WriteOpCode(OpCode.NewArray, 1);
        chunk.WriteByte(3, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        GrobValue top = vm.Stack.Peek();
        Assert.True(top.IsArray);
        GrobArray arr = top.AsArray();
        Assert.Equal(3, arr.Count);
        Assert.Equal(10, arr[0].AsInt());
        Assert.Equal(20, arr[1].AsInt());
        Assert.Equal(30, arr[2].AsInt());
    }

    // -----------------------------------------------------------------------
    // GetIndex — array by int
    // -----------------------------------------------------------------------

    [Fact]
    public void GetIndex_Array_ReturnsElementAtIndex() {
        var arr = new GrobArray([GrobValue.FromInt(10), GrobValue.FromInt(20), GrobValue.FromInt(30)]);
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(arr)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(1)), 1);
        chunk.WriteOpCode(OpCode.GetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(20, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void GetIndex_ArrayOutOfRange_ThrowsE5101() {
        var arr = new GrobArray([GrobValue.FromInt(10)]);
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(arr)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(5)), 1);
        chunk.WriteOpCode(OpCode.GetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));
        Assert.Equal("E5101", ex.Code);
    }

    // -----------------------------------------------------------------------
    // GetIndex — map by string key
    // -----------------------------------------------------------------------

    [Fact]
    public void GetIndex_Map_ReturnsValueForKey() {
        var map = new GrobMap();
        map.Set("a", GrobValue.FromInt(1));
        map.Set("b", GrobValue.FromInt(2));
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("b")), 1);
        chunk.WriteOpCode(OpCode.GetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(2, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void GetIndex_MapMissingKey_ReturnsNil() {
        var map = new GrobMap();
        map.Set("a", GrobValue.FromInt(1));
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("missing")), 1);
        chunk.WriteOpCode(OpCode.GetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().IsNil);
    }

    [Fact]
    public void GetIndex_NilReceiver_ThrowsE5201() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(0)), 1);
        chunk.WriteOpCode(OpCode.GetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));
        Assert.Equal("E5201", ex.Code);
    }

    [Fact]
    public void GetIndex_UnsupportedReceiver_ThrowsInternal() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(7)), 1); // int receiver
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(0)), 1);
        chunk.WriteOpCode(OpCode.GetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        Assert.Throws<GrobInternalException>(() => vm.Run(chunk));
    }

    // -----------------------------------------------------------------------
    // GetProperty — length / keys
    // -----------------------------------------------------------------------

    [Fact]
    public void GetProperty_ArrayLength_ReturnsCount() {
        var arr = new GrobArray([GrobValue.FromInt(7), GrobValue.FromInt(8)]);
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(arr)), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("length")), 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(2, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void GetProperty_MapKeys_ReturnsKeysInInsertionOrder() {
        var map = new GrobMap();
        map.Set("b", GrobValue.FromInt(1));
        map.Set("a", GrobValue.FromInt(2));
        map.Set("c", GrobValue.FromInt(3));
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("keys")), 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        GrobValue top = vm.Stack.Peek();
        Assert.True(top.IsArray);
        GrobArray keys = top.AsArray();
        Assert.Equal(3, keys.Count);
        Assert.Equal("b", keys[0].AsString());
        Assert.Equal("a", keys[1].AsString());
        Assert.Equal("c", keys[2].AsString());
    }

    [Fact]
    public void GetProperty_NilReceiver_ThrowsE5201() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("length")), 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));
        Assert.Equal("E5201", ex.Code);
    }

    [Fact]
    public void GetProperty_UnsupportedReceiver_ThrowsInternal() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromInt(7)), 1); // int receiver
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("length")), 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        Assert.Throws<GrobInternalException>(() => vm.Run(chunk));
    }

    // -----------------------------------------------------------------------
    // Full map iteration — hand-built lowering of `for k, v in m { print(v) }`
    // -----------------------------------------------------------------------

    [Fact]
    public void MapForIn_IteratesInsertionOrder_ValueMatchesKey() {
        var map = new GrobMap();
        map.Set("first", GrobValue.FromInt(10));
        map.Set("second", GrobValue.FromInt(20));
        map.Set("third", GrobValue.FromInt(30));

        var chunk = new Chunk();
        byte mapIdx = ConstByte(chunk, GrobValue.FromMap(map));
        byte keysName = ConstByte(chunk, GrobValue.FromString("keys"));
        byte lenName = ConstByte(chunk, GrobValue.FromString("length"));
        byte zero = ConstByte(chunk, GrobValue.FromInt(0));

        // slot 0 = $map, slot 1 = $keys, slot 2 = $i; body slots 3 = k, 4 = v
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(mapIdx, 1);                // [$map]
        chunk.WriteOpCode(OpCode.GetLocal, 1);
        chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(keysName, 1);              // [$keys]
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(zero, 1);                  // [$i = 0]

        int loopTop = chunk.Count;
        chunk.WriteOpCode(OpCode.GetLocal, 1);
        chunk.WriteByte(2, 1);                     // i
        chunk.WriteOpCode(OpCode.GetLocal, 1);
        chunk.WriteByte(1, 1);                     // keys
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(lenName, 1);               // keys.length
        chunk.WriteOpCode(OpCode.LessInt, 1);      // i < length
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        int exitPatch = chunk.Count;
        chunk.WriteByte(0xFF, 1);
        chunk.WriteByte(0xFF, 1);

        // body: k = keys[i]; v = map[k]; print(v)
        chunk.WriteOpCode(OpCode.GetLocal, 1);
        chunk.WriteByte(1, 1);                     // keys
        chunk.WriteOpCode(OpCode.GetLocal, 1);
        chunk.WriteByte(2, 1);                     // i
        chunk.WriteOpCode(OpCode.GetIndex, 1);     // keys[i] → k (slot 3)
        chunk.WriteOpCode(OpCode.GetLocal, 1);
        chunk.WriteByte(0, 1);                     // map
        chunk.WriteOpCode(OpCode.GetLocal, 1);
        chunk.WriteByte(3, 1);                     // k
        chunk.WriteOpCode(OpCode.GetIndex, 1);     // map[k] → v (slot 4)
        chunk.WriteOpCode(OpCode.GetLocal, 1);
        chunk.WriteByte(4, 1);                     // v
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.PopN, 1);
        chunk.WriteByte(2, 1);                     // pop v, k

        // increment + loop back
        chunk.WriteOpCode(OpCode.IncrementInt, 1);
        chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Loop, 1);
        int loopOffset = (chunk.Count + 2) - loopTop;
        chunk.WriteByte((byte)(loopOffset >> 8), 1);
        chunk.WriteByte((byte)(loopOffset & 0xFF), 1);

        // exit: patch JumpIfFalse, pop synthetics
        int exitTarget = chunk.Count - (exitPatch + 2);
        chunk.PatchByte(exitPatch, (byte)(exitTarget >> 8));
        chunk.PatchByte(exitPatch + 1, (byte)(exitTarget & 0xFF));
        chunk.WriteOpCode(OpCode.PopN, 1);
        chunk.WriteByte(3, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal($"10{Environment.NewLine}20{Environment.NewLine}30{Environment.NewLine}", output.ToString());
    }
}
