using System.Text;
using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// Dispatch-loop tests — all against hand-constructed chunks.
/// Sprint 2 Increment B: no compiler exists yet; chunks are built directly
/// via <see cref="Chunk.WriteOpCode"/> and <see cref="Chunk.AddConstant"/>.
/// </summary>
public sealed class VirtualMachineTests {
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) =>
        (byte)chunk.AddConstant(value);

    // -------------------------------------------------------------------------
    // The 2 + 3 * 4 chunk — the acceptance witness
    // -------------------------------------------------------------------------

    [Fact]
    public void TwoPlusThreeTimesFour_PrintsFourteen() {
        var chunk = new Chunk();
        byte i2 = ConstByte(chunk, GrobValue.FromInt(2));
        byte i3 = ConstByte(chunk, GrobValue.FromInt(3));
        byte i4 = ConstByte(chunk, GrobValue.FromInt(4));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i2, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i3, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i4, 1);
        chunk.WriteOpCode(OpCode.MultiplyInt, 1);
        chunk.WriteOpCode(OpCode.AddInt, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal($"14{Environment.NewLine}", output.ToString());
        Assert.Equal(0, vm.Stack.Count);
    }

    [Fact]
    public void TwoPlusThreeTimesFour_LeavesFourteenOnStack_WhenNoPrint() {
        var chunk = new Chunk();
        byte i2 = ConstByte(chunk, GrobValue.FromInt(2));
        byte i3 = ConstByte(chunk, GrobValue.FromInt(3));
        byte i4 = ConstByte(chunk, GrobValue.FromInt(4));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i2, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i3, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i4, 1);
        chunk.WriteOpCode(OpCode.MultiplyInt, 1);
        chunk.WriteOpCode(OpCode.AddInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(14L, vm.Stack.Peek().AsInt());
    }

    // -------------------------------------------------------------------------
    // Integer arithmetic
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(OpCode.AddInt, 5L, 7L, 12L)]
    [InlineData(OpCode.SubtractInt, 10L, 4L, 6L)]
    [InlineData(OpCode.MultiplyInt, 6L, 7L, 42L)]
    [InlineData(OpCode.DivideInt, 7L, 2L, 3L)]       // truncating
    [InlineData(OpCode.DivideInt, -7L, 2L, -3L)]     // truncating, signed
    [InlineData(OpCode.ModuloInt, 7L, 3L, 1L)]
    [InlineData(OpCode.ModuloInt, -7L, 3L, -1L)]     // sign of dividend
    public void IntArithmetic_BinaryOps_ProduceExpectedResult(OpCode op, long a, long b, long expected) {
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromInt(a));
        byte ib = ConstByte(chunk, GrobValue.FromInt(b));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ia, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ib, 1);
        chunk.WriteOpCode(op, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(expected, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void NegateInt_FlipsSign() {
        var chunk = new Chunk();
        byte i = ConstByte(chunk, GrobValue.FromInt(42));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i, 1);
        chunk.WriteOpCode(OpCode.NegateInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(-42L, vm.Stack.Peek().AsInt());
    }

    // -------------------------------------------------------------------------
    // Float arithmetic
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(OpCode.AddFloat, 1.5, 2.25, 3.75)]
    [InlineData(OpCode.SubtractFloat, 3.5, 1.25, 2.25)]
    [InlineData(OpCode.MultiplyFloat, 2.0, 2.5, 5.0)]
    [InlineData(OpCode.DivideFloat, 7.0, 2.0, 3.5)]
    [InlineData(OpCode.ModuloFloat, 7.5, 2.0, 1.5)]
    public void FloatArithmetic_BinaryOps_ProduceExpectedResult(OpCode op, double a, double b, double expected) {
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromFloat(a));
        byte ib = ConstByte(chunk, GrobValue.FromFloat(b));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ia, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ib, 1);
        chunk.WriteOpCode(op, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(expected, vm.Stack.Peek().AsFloat());
    }

    [Fact]
    public void NegateFloat_FlipsSign() {
        var chunk = new Chunk();
        byte i = ConstByte(chunk, GrobValue.FromFloat(3.14));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i, 1);
        chunk.WriteOpCode(OpCode.NegateFloat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(-3.14, vm.Stack.Peek().AsFloat());
    }

    [Fact]
    public void IntToFloat_PromotesIntegerToDouble() {
        var chunk = new Chunk();
        byte i = ConstByte(chunk, GrobValue.FromInt(7));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i, 1);
        chunk.WriteOpCode(OpCode.IntToFloat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().IsFloat);
        Assert.Equal(7.0, vm.Stack.Peek().AsFloat());
    }

    // -------------------------------------------------------------------------
    // Concat
    // -------------------------------------------------------------------------

    [Fact]
    public void Concat_JoinsTwoStrings() {
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromString("hello, "));
        byte ib = ConstByte(chunk, GrobValue.FromString("world"));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ia, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ib, 1);
        chunk.WriteOpCode(OpCode.Concat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal("hello, world", vm.Stack.Peek().AsString());
    }

    // -------------------------------------------------------------------------
    // Arithmetic errors — carry the correct source line
    // -------------------------------------------------------------------------

    [Fact]
    public void IntOverflow_OnAdd_ThrowsArithmeticError_WithLine() {
        const int line = 7;
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromInt(long.MaxValue));
        byte ib = ConstByte(chunk, GrobValue.FromInt(1));
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ia, line);
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ib, line);
        chunk.WriteOpCode(OpCode.AddInt, line);
        chunk.WriteOpCode(OpCode.Return, line);

        var (vm, _) = NewVm();
        var ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal("E5001", ex.Code);
        Assert.Equal(line, ex.Line);
    }

    [Fact]
    public void IntOverflow_OnNegateMinValue_ThrowsArithmeticError() {
        const int line = 3;
        var chunk = new Chunk();
        byte i = ConstByte(chunk, GrobValue.FromInt(long.MinValue));
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(i, line);
        chunk.WriteOpCode(OpCode.NegateInt, line);
        chunk.WriteOpCode(OpCode.Return, line);

        var (vm, _) = NewVm();
        var ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal("E5001", ex.Code);
        Assert.Equal(line, ex.Line);
    }

    [Fact]
    public void IntDivideByZero_ThrowsE5002_WithLine() {
        const int line = 11;
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromInt(10));
        byte ib = ConstByte(chunk, GrobValue.FromInt(0));
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ia, line);
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ib, line);
        chunk.WriteOpCode(OpCode.DivideInt, line);
        chunk.WriteOpCode(OpCode.Return, line);

        var (vm, _) = NewVm();
        var ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal("E5002", ex.Code);
        Assert.Equal(line, ex.Line);
    }

    [Fact]
    public void IntModuloByZero_ThrowsE5003() {
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromInt(10));
        byte ib = ConstByte(chunk, GrobValue.FromInt(0));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ia, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ib, 1);
        chunk.WriteOpCode(OpCode.ModuloInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        var ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal("E5003", ex.Code);
    }

    [Fact]
    public void FloatDivideByZero_ThrowsE5004() {
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromFloat(1.0));
        byte ib = ConstByte(chunk, GrobValue.FromFloat(0.0));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ia, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ib, 1);
        chunk.WriteOpCode(OpCode.DivideFloat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        var ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal("E5004", ex.Code);
    }

    [Fact]
    public void FloatModuloByZero_ThrowsE5005() {
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromFloat(1.0));
        byte ib = ConstByte(chunk, GrobValue.FromFloat(0.0));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ia, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ib, 1);
        chunk.WriteOpCode(OpCode.ModuloFloat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        var ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal("E5005", ex.Code);
    }

    [Fact]
    public void VmStopsOnFirstRuntimeError_SubsequentInstructionsNotExecuted() {
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromInt(1));
        byte ib = ConstByte(chunk, GrobValue.FromInt(0));
        byte hello = ConstByte(chunk, GrobValue.FromString("should-not-print"));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ia, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ib, 1);
        chunk.WriteOpCode(OpCode.DivideInt, 1);
        chunk.WriteOpCode(OpCode.Constant, 2); chunk.WriteByte(hello, 2);
        chunk.WriteOpCode(OpCode.Print, 2);
        chunk.WriteOpCode(OpCode.Return, 2);

        var (vm, output) = NewVm();
        Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal(string.Empty, output.ToString());
    }

    // -------------------------------------------------------------------------
    // Print display forms
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(GrobValueKind.Int, "42")]
    [InlineData(GrobValueKind.Float, "3.14")]
    [InlineData(GrobValueKind.Bool, "true")]
    [InlineData(GrobValueKind.String, "hello")]
    [InlineData(GrobValueKind.Nil, "nil")]
    public void Print_WritesDisplayFormOfScalarKinds(GrobValueKind kind, string expected) {
        var chunk = new Chunk();
        GrobValue value = kind switch {
            GrobValueKind.Int => GrobValue.FromInt(42),
            GrobValueKind.Float => GrobValue.FromFloat(3.14),
            GrobValueKind.Bool => GrobValue.FromBool(true),
            GrobValueKind.String => GrobValue.FromString("hello"),
            GrobValueKind.Nil => GrobValue.Nil,
            _ => throw new InvalidOperationException(),
        };

        if (kind == GrobValueKind.Nil) {
            chunk.WriteOpCode(OpCode.Nil, 1);
        } else {
            byte ci = ConstByte(chunk, value);
            chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        }
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal(expected + Environment.NewLine, output.ToString());
    }

    // -------------------------------------------------------------------------
    // ValueStack overflow surfaces as a runtime error
    // -------------------------------------------------------------------------

    [Fact]
    public void ValueStackOverflow_SurfacesAsRuntimeError_NotUnguardedWrite() {
        var stack = new ValueStack();
        for (int i = 0; i < ValueStack.Capacity; i++)
            stack.Push(GrobValue.FromInt(i), line: 1);

        var ex = Assert.Throws<GrobRuntimeException>(
            () => stack.Push(GrobValue.FromInt(0), line: 99));
        Assert.Equal(99, ex.Line);
        Assert.Equal(ValueStack.Capacity, stack.Count);
    }

    [Fact]
    public void ValueStack_PushPopPeek_Roundtrip() {
        var stack = new ValueStack();
        stack.Push(GrobValue.FromInt(1), 1);
        stack.Push(GrobValue.FromInt(2), 1);
        stack.Push(GrobValue.FromInt(3), 1);
        Assert.Equal(3, stack.Count);
        Assert.Equal(3L, stack.Peek(0).AsInt());
        Assert.Equal(2L, stack.Peek(1).AsInt());
        Assert.Equal(1L, stack.Peek(2).AsInt());
        Assert.Equal(3L, stack.Pop().AsInt());
        Assert.Equal(2L, stack.Pop().AsInt());
        Assert.Equal(1L, stack.Pop().AsInt());
        Assert.Equal(0, stack.Count);
    }

    // -------------------------------------------------------------------------
    // End-without-Return: malformed chunk → GrobInternalException
    // -------------------------------------------------------------------------

    [Fact]
    public void ChunkEndingWithoutReturn_RaisesInternalException() {
        var chunk = new Chunk();
        byte i = ConstByte(chunk, GrobValue.FromInt(1));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i, 1);
        // No Return appended — deliberately malformed.

        var (vm, _) = NewVm();
        Assert.Throws<GrobInternalException>(() => vm.Run(chunk));
    }

    // -------------------------------------------------------------------------
    // Constants and singletons
    // -------------------------------------------------------------------------

    [Fact]
    public void ConstantLong_LoadsFromTwoByteBigEndianIndex() {
        var chunk = new Chunk();
        // Fill the pool until we need a 2-byte index.
        const int target = 300;
        int targetIndex = -1;
        for (int i = 0; i <= target; i++) {
            int idx = chunk.AddConstant(GrobValue.FromInt(i));
            if (i == target) targetIndex = idx;
        }
        chunk.WriteOpCode(OpCode.ConstantLong, 1);
        chunk.WriteByte((byte)((targetIndex >> 8) & 0xFF), 1);
        chunk.WriteByte((byte)(targetIndex & 0xFF), 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal((long)target, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void NilTrueFalse_PushSingletons() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.True, 1);
        chunk.WriteOpCode(OpCode.False, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(3, vm.Stack.Count);
        Assert.False(vm.Stack.Peek(0).AsBool());
        Assert.True(vm.Stack.Peek(1).AsBool());
        Assert.True(vm.Stack.Peek(2).IsNil);
    }

    [Fact]
    public void Pop_DiscardsTopOfStack() {
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromInt(1));
        byte ib = ConstByte(chunk, GrobValue.FromInt(2));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ia, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ib, 1);
        chunk.WriteOpCode(OpCode.Pop, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(1L, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void PopN_DiscardsCountValues() {
        var chunk = new Chunk();
        for (int i = 0; i < 4; i++) {
            byte ci = ConstByte(chunk, GrobValue.FromInt(i));
            chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        }
        chunk.WriteOpCode(OpCode.PopN, 1); chunk.WriteByte(3, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(0L, vm.Stack.Peek().AsInt());
    }

#if DEBUG
    // -------------------------------------------------------------------------
    // D-306 trace hook — Debug-only behaviour assertion
    // -------------------------------------------------------------------------

    [Fact]
    public void TraceInstruction_InDebug_WritesStackAndDisassemblyEveryIteration() {
        var chunk = new Chunk();
        byte i2 = ConstByte(chunk, GrobValue.FromInt(2));
        byte i3 = ConstByte(chunk, GrobValue.FromInt(3));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i2, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i3, 1);
        chunk.WriteOpCode(OpCode.AddInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var output = new StringWriter();
        var trace = new StringWriter();
        var vm = new VirtualMachine(output, trace);
        vm.Run(chunk);

        string traced = trace.ToString();
        // Stack rendering markers and opcode names from the disassembler.
        Assert.Contains("[ 2 ]", traced);
        Assert.Contains("[ 2 ][ 3 ]", traced);
        Assert.Contains("Constant", traced);
        Assert.Contains("AddInt", traced);
        Assert.Contains("Return", traced);
    }
#endif
}
