using Grob.Core;
using Grob.Vm;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 6 Increment D — <see cref="OpCode.NewAnonStruct"/>.
/// All chunks are hand-built; no compiler is involved so the opcode arm is
/// tested in isolation.
/// </summary>
public sealed class VirtualMachineAnonStructTests {
    private static (VirtualMachine Vm, StringWriter Output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    // -----------------------------------------------------------------------
    // Basic construction — two name/value pairs
    // -----------------------------------------------------------------------

    [Fact]
    public void NewAnonStruct_TwoFields_ProducesCorrectGrobStruct() {
        var chunk = new Chunk();

        // Stack layout: name1, val1, name2, val2 (bottom→top), then NewAnonStruct 2.
        byte nameConst = (byte)chunk.AddConstant(GrobValue.FromString("name"));
        byte valConst = (byte)chunk.AddConstant(GrobValue.FromString("Alice"));
        byte ageConst = (byte)chunk.AddConstant(GrobValue.FromString("age"));
        byte ageVal = (byte)chunk.AddConstant(GrobValue.FromInt(30));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(nameConst, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(valConst, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ageConst, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ageVal, 1);
        chunk.WriteOpCode(OpCode.NewAnonStruct, 1); chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        GrobValue result = vm.Stack.Peek();
        Assert.True(result.IsStruct, "expected Struct on the stack");
        GrobStruct s = result.AsStruct();
        Assert.Equal("Alice", s.GetField("name").AsString());
        Assert.Equal(30L, s.GetField("age").AsInt());
    }

    // -----------------------------------------------------------------------
    // Zero-field anonymous struct
    // -----------------------------------------------------------------------

    [Fact]
    public void NewAnonStruct_ZeroFields_ProducesEmptyStruct() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.NewAnonStruct, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        GrobValue result = vm.Stack.Peek();
        Assert.True(result.IsStruct);
    }

    // -----------------------------------------------------------------------
    // Stack depth — name/value pairs consumed, one struct left
    // -----------------------------------------------------------------------

    [Fact]
    public void NewAnonStruct_TwoFields_LeavesExactlyOneValueOnStack() {
        var chunk = new Chunk();

        byte n1 = (byte)chunk.AddConstant(GrobValue.FromString("x"));
        byte v1 = (byte)chunk.AddConstant(GrobValue.FromInt(1));
        byte n2 = (byte)chunk.AddConstant(GrobValue.FromString("y"));
        byte v2 = (byte)chunk.AddConstant(GrobValue.FromInt(2));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(n1, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(v1, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(n2, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(v2, 1);
        chunk.WriteOpCode(OpCode.NewAnonStruct, 1); chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // 4 name+value pushes consumed by NewAnonStruct → exactly 1 struct.
        Assert.Equal(1, vm.Stack.Count);
    }

    // -----------------------------------------------------------------------
    // Disassembler — NewAnonStruct appears in the disassembly output
    // -----------------------------------------------------------------------

    [Fact]
    public void NewAnonStruct_Disassembly_ContainsNewAnonStruct() {
        var chunk = new Chunk();
        byte n = (byte)chunk.AddConstant(GrobValue.FromString("x"));
        byte v = (byte)chunk.AddConstant(GrobValue.FromInt(1));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(n, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(v, 1);
        chunk.WriteOpCode(OpCode.NewAnonStruct, 1); chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        using var writer = new StringWriter();
        Disassembler.DisassembleChunk(chunk, writer, "test");
        string output = writer.ToString();

        Assert.Contains("NewAnonStruct", output);
    }

    // -----------------------------------------------------------------------
    // Field retrieval after construction
    // -----------------------------------------------------------------------

    [Fact]
    public void NewAnonStruct_Fields_AreRetrievableByName() {
        var chunk = new Chunk();

        byte kScore = (byte)chunk.AddConstant(GrobValue.FromString("score"));
        byte vScore = (byte)chunk.AddConstant(GrobValue.FromFloat(9.5));
        byte kLabel = (byte)chunk.AddConstant(GrobValue.FromString("label"));
        byte vLabel = (byte)chunk.AddConstant(GrobValue.FromString("A+"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(kScore, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(vScore, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(kLabel, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(vLabel, 1);
        chunk.WriteOpCode(OpCode.NewAnonStruct, 1); chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        GrobStruct s = vm.Stack.Peek().AsStruct();
        Assert.Equal(9.5, s.GetField("score").AsFloat(), precision: 10);
        Assert.Equal("A+", s.GetField("label").AsString());
    }
}
