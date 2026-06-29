using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 6 Increment B — <see cref="OpCode.NewStruct"/>.
/// All chunks are hand-built; no compiler is involved so the opcode arm is
/// tested in isolation.
/// </summary>
public sealed class VirtualMachineStructTests {
    private static (VirtualMachine Vm, StringWriter Output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    // -----------------------------------------------------------------------
    // Basic construction — two fields
    // -----------------------------------------------------------------------

    [Fact]
    public void NewStruct_TwoFields_ProducesCorrectGrobStruct() {
        var chunk = new Chunk();
        byte typeIdx = chunk.AddStructType(new StructTypeDescriptor("Config", ["host", "port"]));

        // Push host then port (declaration order)
        byte hostConst = (byte)chunk.AddConstant(GrobValue.FromString("localhost"));
        byte portConst = (byte)chunk.AddConstant(GrobValue.FromInt(8080));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(hostConst, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(portConst, 1);
        chunk.WriteOpCode(OpCode.NewStruct, 1); chunk.WriteByte(typeIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        GrobValue result = vm.Stack.Peek();
        Assert.True(result.IsStruct, "expected Struct on the stack");
        GrobStruct s = result.AsStruct();
        Assert.Equal("Config", s.TypeName);
        Assert.Equal("localhost", s.GetField("host").AsString());
        Assert.Equal(8080L, s.GetField("port").AsInt());
    }

    // -----------------------------------------------------------------------
    // Zero-field struct
    // -----------------------------------------------------------------------

    [Fact]
    public void NewStruct_ZeroFields_ProducesEmptyStruct() {
        var chunk = new Chunk();
        byte typeIdx = chunk.AddStructType(new StructTypeDescriptor("Empty", []));

        chunk.WriteOpCode(OpCode.NewStruct, 1); chunk.WriteByte(typeIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        GrobValue result = vm.Stack.Peek();
        Assert.True(result.IsStruct);
        Assert.Equal("Empty", result.AsStruct().TypeName);
    }

    // -----------------------------------------------------------------------
    // Stack depth — construction consumes field values and leaves one struct
    // -----------------------------------------------------------------------

    [Fact]
    public void NewStruct_ConsumesTwoFieldsAndLeavesSingleValue() {
        var chunk = new Chunk();
        byte typeIdx = chunk.AddStructType(new StructTypeDescriptor("Pair", ["a", "b"]));
        byte c1 = (byte)chunk.AddConstant(GrobValue.FromInt(1));
        byte c2 = (byte)chunk.AddConstant(GrobValue.FromInt(2));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(c1, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(c2, 1);
        chunk.WriteOpCode(OpCode.NewStruct, 1); chunk.WriteByte(typeIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Stack had 2 field values; NewStruct consumes them and pushes 1 struct.
        Assert.Equal(1, vm.Stack.Count);
    }

    // -----------------------------------------------------------------------
    // Field order — LIFO pop reconstructs declaration order
    // -----------------------------------------------------------------------

    [Fact]
    public void NewStruct_FieldsInDeclarationOrder_MatchDescriptorOrder() {
        // Push fields in declaration order (first=host, last=port).
        // VM pops LIFO so should correctly map: pop first=port, pop second=host.
        var chunk = new Chunk();
        byte typeIdx = chunk.AddStructType(new StructTypeDescriptor("Config", ["host", "port"]));
        byte hostConst = (byte)chunk.AddConstant(GrobValue.FromString("example.com"));
        byte portConst = (byte)chunk.AddConstant(GrobValue.FromInt(443));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(hostConst, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(portConst, 1);
        chunk.WriteOpCode(OpCode.NewStruct, 1); chunk.WriteByte(typeIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        GrobStruct s = vm.Stack.Peek().AsStruct();
        Assert.Equal("example.com", s.GetField("host").AsString());
        Assert.Equal(443L, s.GetField("port").AsInt());
    }
}
