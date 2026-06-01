using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM-level tests for Sprint 3 Increment A variable opcodes: DefineGlobal,
/// GetGlobal, SetGlobal, GetLocal, SetLocal, IncrementInt, DecrementInt, PopN.
/// All chunks are hand-constructed (no compiler dependency).
/// </summary>
public sealed class VirtualMachineVariableTests {
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte NameByte(Chunk chunk, string name) =>
        (byte)chunk.AddConstant(GrobValue.FromString(name));

    private static byte IntByte(Chunk chunk, long value) =>
        (byte)chunk.AddConstant(GrobValue.FromInt(value));

    // -------------------------------------------------------------------------
    // DefineGlobal / GetGlobal
    // -------------------------------------------------------------------------

    [Fact]
    public void DefineGlobal_ThenGetGlobal_PrintsValue() {
        // x := 42; print(x)
        var chunk = new Chunk();
        byte xName = NameByte(chunk, "x");
        byte i42 = IntByte(chunk, 42L);
        // DefineGlobal x = 42
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i42, 1);
        chunk.WriteOpCode(OpCode.DefineGlobal, 1); chunk.WriteByte(xName, 1);
        // GetGlobal x → Print
        chunk.WriteOpCode(OpCode.GetGlobal, 2); chunk.WriteByte(xName, 2);
        chunk.WriteOpCode(OpCode.Print, 2);
        chunk.WriteOpCode(OpCode.Return, 2);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal($"42{Environment.NewLine}", output.ToString());
        Assert.Equal(0, vm.Stack.Count);
    }

    [Fact]
    public void DefineGlobal_IsStoredInGlobalsDict() {
        var chunk = new Chunk();
        byte xName = NameByte(chunk, "answer");
        byte val = IntByte(chunk, 99L);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(val, 1);
        chunk.WriteOpCode(OpCode.DefineGlobal, 1); chunk.WriteByte(xName, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Globals.TryGetValue("answer", out GrobValue answerValue));
        Assert.Equal(99L, answerValue.AsInt());
    }

    // -------------------------------------------------------------------------
    // SetGlobal
    // -------------------------------------------------------------------------

    [Fact]
    public void SetGlobal_UpdatesExistingGlobal() {
        // x := 1; x = 2; print(x)
        var chunk = new Chunk();
        byte xName = NameByte(chunk, "x");
        byte i1 = IntByte(chunk, 1L);
        byte i2 = IntByte(chunk, 2L);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i1, 1);
        chunk.WriteOpCode(OpCode.DefineGlobal, 1); chunk.WriteByte(xName, 1);
        chunk.WriteOpCode(OpCode.Constant, 2); chunk.WriteByte(i2, 2);
        chunk.WriteOpCode(OpCode.SetGlobal, 2); chunk.WriteByte(xName, 2);
        chunk.WriteOpCode(OpCode.GetGlobal, 3); chunk.WriteByte(xName, 3);
        chunk.WriteOpCode(OpCode.Print, 3);
        chunk.WriteOpCode(OpCode.Return, 3);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal($"2{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public void GetGlobal_UndefinedName_ThrowsGrobRuntimeException() {
        var chunk = new Chunk();
        byte xName = NameByte(chunk, "notDefined");
        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte(xName, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));
        Assert.Equal("E1001", ex.Code);
    }

    // -------------------------------------------------------------------------
    // GetLocal / SetLocal
    // -------------------------------------------------------------------------

    [Fact]
    public void GetLocal_ReadsValueFromSlot() {
        // Push 7 (slot 0 = local), then GetLocal 0 → Print
        var chunk = new Chunk();
        byte i7 = (byte)chunk.AddConstant(GrobValue.FromInt(7L));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i7, 1);   // slot 0 = 7
        chunk.WriteOpCode(OpCode.GetLocal, 2); chunk.WriteByte(0, 2);    // push copy
        chunk.WriteOpCode(OpCode.Print, 2);
        // PopN to discard the local before Return
        chunk.WriteOpCode(OpCode.PopN, 3); chunk.WriteByte(1, 3);
        chunk.WriteOpCode(OpCode.Return, 3);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal($"7{Environment.NewLine}", output.ToString());
        Assert.Equal(0, vm.Stack.Count);
    }

    [Fact]
    public void SetLocal_UpdatesSlotAndLeavesStackClean() {
        // slot 0 = 10; SetLocal 0 <- 20; GetLocal 0 → Print
        var chunk = new Chunk();
        byte i10 = (byte)chunk.AddConstant(GrobValue.FromInt(10L));
        byte i20 = (byte)chunk.AddConstant(GrobValue.FromInt(20L));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i10, 1);  // slot 0 = 10
        chunk.WriteOpCode(OpCode.Constant, 2); chunk.WriteByte(i20, 2);  // push 20
        chunk.WriteOpCode(OpCode.SetLocal, 2); chunk.WriteByte(0, 2);     // slot 0 = 20, pops 20
        chunk.WriteOpCode(OpCode.GetLocal, 3); chunk.WriteByte(0, 3);
        chunk.WriteOpCode(OpCode.Print, 3);
        chunk.WriteOpCode(OpCode.PopN, 4); chunk.WriteByte(1, 4);
        chunk.WriteOpCode(OpCode.Return, 4);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal($"20{Environment.NewLine}", output.ToString());
        Assert.Equal(0, vm.Stack.Count);
    }

    // -------------------------------------------------------------------------
    // IncrementInt / DecrementInt
    // -------------------------------------------------------------------------

    [Fact]
    public void IncrementInt_IncrementsLocalSlot() {
        var chunk = new Chunk();
        byte i5 = (byte)chunk.AddConstant(GrobValue.FromInt(5L));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i5, 1);   // slot 0 = 5
        chunk.WriteOpCode(OpCode.IncrementInt, 2); chunk.WriteByte(0, 2); // slot 0 = 6
        chunk.WriteOpCode(OpCode.GetLocal, 2); chunk.WriteByte(0, 2);
        chunk.WriteOpCode(OpCode.Print, 2);
        chunk.WriteOpCode(OpCode.PopN, 3); chunk.WriteByte(1, 3);
        chunk.WriteOpCode(OpCode.Return, 3);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal($"6{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public void DecrementInt_DecrementsLocalSlot() {
        var chunk = new Chunk();
        byte i3 = (byte)chunk.AddConstant(GrobValue.FromInt(3L));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i3, 1);   // slot 0 = 3
        chunk.WriteOpCode(OpCode.DecrementInt, 2); chunk.WriteByte(0, 2); // slot 0 = 2
        chunk.WriteOpCode(OpCode.GetLocal, 2); chunk.WriteByte(0, 2);
        chunk.WriteOpCode(OpCode.Print, 2);
        chunk.WriteOpCode(OpCode.PopN, 3); chunk.WriteByte(1, 3);
        chunk.WriteOpCode(OpCode.Return, 3);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal($"2{Environment.NewLine}", output.ToString());
    }

    // -------------------------------------------------------------------------
    // PopN
    // -------------------------------------------------------------------------

    [Fact]
    public void PopN_DiscardsNStackItems() {
        var chunk = new Chunk();
        byte i1 = (byte)chunk.AddConstant(GrobValue.FromInt(1L));
        byte i2 = (byte)chunk.AddConstant(GrobValue.FromInt(2L));
        byte i3 = (byte)chunk.AddConstant(GrobValue.FromInt(3L));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i1, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i2, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i3, 1);
        // Stack = [1, 2, 3]
        chunk.WriteOpCode(OpCode.PopN, 2); chunk.WriteByte(3, 2);
        // Stack = []
        chunk.WriteOpCode(OpCode.Return, 3);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(0, vm.Stack.Count);
    }
}
