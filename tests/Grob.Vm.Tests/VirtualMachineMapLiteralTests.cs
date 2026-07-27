using Grob.Core;
using Grob.Vm;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for <see cref="OpCode.NewMap"/> (D-376). All chunks are hand-built; no
/// compiler is involved so the opcode arm is tested in isolation, mirroring
/// <c>VirtualMachineAnonStructTests</c>.
/// </summary>
public sealed class VirtualMachineMapLiteralTests {
    private static (VirtualMachine Vm, StringWriter Output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    // -----------------------------------------------------------------------
    // Basic construction — two key/value pairs
    // -----------------------------------------------------------------------

    [Fact]
    public void NewMap_TwoEntries_ProducesCorrectGrobMap() {
        var chunk = new Chunk();

        // Stack layout: key1, val1, key2, val2 (bottom→top), then NewMap 2.
        byte keyA = (byte)chunk.AddConstant(GrobValue.FromString("a"));
        byte valA = (byte)chunk.AddConstant(GrobValue.FromInt(1));
        byte keyB = (byte)chunk.AddConstant(GrobValue.FromString("b"));
        byte valB = (byte)chunk.AddConstant(GrobValue.FromInt(2));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(keyA, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(valA, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(keyB, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(valB, 1);
        chunk.WriteOpCode(OpCode.NewMap, 1); chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        GrobValue result = vm.Stack.Peek();
        Assert.True(result.IsMap, "expected Map on the stack");
        GrobMap map = result.AsMap();
        Assert.Equal(1L, map["a"].AsInt());
        Assert.Equal(2L, map["b"].AsInt());
    }

    [Fact]
    public void NewMap_PreservesSourceEntryOrder() {
        var chunk = new Chunk();

        byte keyHost = (byte)chunk.AddConstant(GrobValue.FromString("host"));
        byte valHost = (byte)chunk.AddConstant(GrobValue.FromString("example.com"));
        byte keyPort = (byte)chunk.AddConstant(GrobValue.FromString("port"));
        byte valPort = (byte)chunk.AddConstant(GrobValue.FromString("8080"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(keyHost, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(valHost, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(keyPort, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(valPort, 1);
        chunk.WriteOpCode(OpCode.NewMap, 1); chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        GrobMap map = vm.Stack.Peek().AsMap();
        Assert.Equal(["host", "port"], map.InsertionOrderKeys);
    }

    // -----------------------------------------------------------------------
    // Zero-entry map
    // -----------------------------------------------------------------------

    [Fact]
    public void NewMap_ZeroEntries_ProducesEmptyMap() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.NewMap, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        GrobValue result = vm.Stack.Peek();
        Assert.True(result.IsMap);
        Assert.Empty(result.AsMap().InsertionOrderKeys);
    }

    // -----------------------------------------------------------------------
    // Stack depth — key/value pairs consumed, one map left
    // -----------------------------------------------------------------------

    [Fact]
    public void NewMap_TwoEntries_LeavesExactlyOneValueOnStack() {
        var chunk = new Chunk();

        byte k1 = (byte)chunk.AddConstant(GrobValue.FromString("x"));
        byte v1 = (byte)chunk.AddConstant(GrobValue.FromInt(1));
        byte k2 = (byte)chunk.AddConstant(GrobValue.FromString("y"));
        byte v2 = (byte)chunk.AddConstant(GrobValue.FromInt(2));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(k1, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(v1, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(k2, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(v2, 1);
        chunk.WriteOpCode(OpCode.NewMap, 1); chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // 4 key+value pushes consumed by NewMap → exactly 1 map.
        Assert.Equal(1, vm.Stack.Count);
    }

    // -----------------------------------------------------------------------
    // Duplicate key at runtime — last write wins (mirrors GrobMap.Set semantics)
    // -----------------------------------------------------------------------

    [Fact]
    public void NewMap_DuplicateKey_LastValueWins() {
        var chunk = new Chunk();

        byte key = (byte)chunk.AddConstant(GrobValue.FromString("a"));
        byte v1 = (byte)chunk.AddConstant(GrobValue.FromInt(1));
        byte v2 = (byte)chunk.AddConstant(GrobValue.FromInt(2));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(key, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(v1, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(key, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(v2, 1);
        chunk.WriteOpCode(OpCode.NewMap, 1); chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        GrobMap map = vm.Stack.Peek().AsMap();
        Assert.Equal(2L, map["a"].AsInt());
    }

    // -----------------------------------------------------------------------
    // Disassembler — NewMap appears in the disassembly output
    // -----------------------------------------------------------------------

    [Fact]
    public void NewMap_Disassembly_ContainsNewMap() {
        var chunk = new Chunk();
        byte k = (byte)chunk.AddConstant(GrobValue.FromString("a"));
        byte v = (byte)chunk.AddConstant(GrobValue.FromInt(1));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(k, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(v, 1);
        chunk.WriteOpCode(OpCode.NewMap, 1); chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        using var writer = new StringWriter();
        Disassembler.DisassembleChunk(chunk, writer, "test");
        string output = writer.ToString();

        Assert.Contains("NewMap", output);
    }
}
