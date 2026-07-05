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
    public void TwoPlusThreeTimesFour_LeavesFourteenOnStackWhenNoPrint() {
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
    public void IntArithmetic_BinaryOpsProduceExpectedResult(OpCode op, long a, long b, long expected) {
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
    public void FloatArithmetic_BinaryOpsProduceExpectedResult(OpCode op, double a, double b, double expected) {
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
    public void IntOverflowOnAdd_ThrowsArithmeticErrorWithLine() {
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
    public void IntOverflowOnNegateMinValue_ThrowsArithmeticError() {
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
    public void IntDivideByZero_ThrowsE5002WithLine() {
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
        const int line = 1;
        const int column = 9;
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromInt(10));
        byte ib = ConstByte(chunk, GrobValue.FromInt(0));
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ia, line);
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ib, line);
        chunk.WriteOpCode(OpCode.ModuloInt, line, column);
        chunk.WriteOpCode(OpCode.Return, line);

        var (vm, _) = NewVm();
        var ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal("E5003", ex.Code);
        Assert.Equal(line, ex.Line);
        Assert.Equal(column, ex.Column);
    }

    [Fact]
    public void FloatDivideByZero_ThrowsE5004() {
        const int line = 1;
        const int column = 11;
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromFloat(1.0));
        byte ib = ConstByte(chunk, GrobValue.FromFloat(0.0));
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ia, line);
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ib, line);
        chunk.WriteOpCode(OpCode.DivideFloat, line, column);
        chunk.WriteOpCode(OpCode.Return, line);

        var (vm, _) = NewVm();
        var ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal("E5004", ex.Code);
        Assert.Equal(line, ex.Line);
        Assert.Equal(column, ex.Column);
    }

    [Fact]
    public void FloatModuloByZero_ThrowsE5005() {
        const int line = 1;
        const int column = 13;
        var chunk = new Chunk();
        byte ia = ConstByte(chunk, GrobValue.FromFloat(1.0));
        byte ib = ConstByte(chunk, GrobValue.FromFloat(0.0));
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ia, line);
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ib, line);
        chunk.WriteOpCode(OpCode.ModuloFloat, line, column);
        chunk.WriteOpCode(OpCode.Return, line);

        var (vm, _) = NewVm();
        var ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(chunk));
        Assert.Equal("E5005", ex.Code);
        Assert.Equal(line, ex.Line);
        Assert.Equal(column, ex.Column);
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
    public void ValueStackOverflow_SurfacesAsRuntimeErrorNotUnguardedWrite() {
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
    public void TraceInstructionInDebug_WritesStackAndDisassemblyEveryIteration() {
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

    // -----------------------------------------------------------------------
    // OpCode.Exit — D-110 (added in QA pass sprint 3)
    // -----------------------------------------------------------------------

    [Fact]
    public void Exit_ThrowsGrobExitExceptionWithCode() {
        // A chunk that pushes 2 then executes Exit must throw GrobExitException(2).
        var chunk = new Chunk();
        byte c = ConstByte(chunk, GrobValue.FromInt(2));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(c, 1);
        chunk.WriteOpCode(OpCode.Exit, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobExitException ex = Assert.Throws<GrobExitException>(() => vm.Run(chunk));
        Assert.Equal(2, ex.Code);
    }

    [Fact]
    public void Exit_CodeZero_ThrowsWithZero() {
        var chunk = new Chunk();
        byte c = ConstByte(chunk, GrobValue.FromInt(0));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(c, 1);
        chunk.WriteOpCode(OpCode.Exit, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobExitException ex = Assert.Throws<GrobExitException>(() => vm.Run(chunk));
        Assert.Equal(0, ex.Code);
    }

    // -------------------------------------------------------------------------
    // select / case — the equality ladder (Sprint 4 Increment D)
    //
    // The VM gains no new opcode for select; these tests prove the existing
    // Equal / JumpIfFalse / Jump / GetLocal / PopN opcodes execute the ladder
    // shape the compiler emits: subject evaluated once into a synthetic local,
    // first-match with no fall-through, optional default tail, multi-value cases
    // ORed to one block, and a no-op when nothing matches.
    // -------------------------------------------------------------------------

    private sealed record SelectCase(long[] Patterns, long Marker);

    private static int EmitJumpSite(Chunk c, OpCode op) {
        c.WriteOpCode(op, 1);
        int site = c.Count;
        c.WriteByte(0xFF, 1);
        c.WriteByte(0xFF, 1);
        return site;
    }

    private static void PatchJumpSite(Chunk c, int site) {
        int offset = c.Count - (site + 2);
        c.PatchByte(site, (byte)(offset >> 8));
        c.PatchByte(site + 1, (byte)(offset & 0xFF));
    }

    private static void EmitMarker(Chunk c, long marker) {
        byte k = ConstByte(c, GrobValue.FromInt(marker));
        c.WriteOpCode(OpCode.Constant, 1);
        c.WriteByte(k, 1);
        c.WriteOpCode(OpCode.Print, 1);
    }

    /// <summary>
    /// Builds and runs a chunk shaped exactly like the compiler's <c>select</c>
    /// emission — subject in slot 0, an equality ladder over the cases, an optional
    /// default tail, a trailing <see cref="OpCode.PopN"/> — and returns the printed
    /// markers. Each case body prints its marker, so the output identifies which
    /// branch ran.
    /// </summary>
    private static string RunSelect(long subject, SelectCase[] cases, long? defaultMarker) {
        var chunk = new Chunk();
        byte subjConst = ConstByte(chunk, GrobValue.FromInt(subject));
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(subjConst, 1); // subject -> slot 0

        var endJumps = new List<int>();
        foreach (SelectCase clause in cases) {
            var orJumps = new List<int>();
            int nextCaseSite = -1;
            for (int i = 0; i < clause.Patterns.Length; i++) {
                chunk.WriteOpCode(OpCode.GetLocal, 1);
                chunk.WriteByte(0, 1); // slot 0 = subject
                byte p = ConstByte(chunk, GrobValue.FromInt(clause.Patterns[i]));
                chunk.WriteOpCode(OpCode.Constant, 1);
                chunk.WriteByte(p, 1);
                chunk.WriteOpCode(OpCode.Equal, 1);
                if (i < clause.Patterns.Length - 1) {
                    int jf = EmitJumpSite(chunk, OpCode.JumpIfFalse); // not equal -> next pattern
                    orJumps.Add(EmitJumpSite(chunk, OpCode.Jump));    // equal -> body
                    PatchJumpSite(chunk, jf);
                } else {
                    nextCaseSite = EmitJumpSite(chunk, OpCode.JumpIfFalse); // not equal -> next case
                }
            }

            // Body — all OR-jumps converge here.
            foreach (int site in orJumps) PatchJumpSite(chunk, site);
            EmitMarker(chunk, clause.Marker);
            endJumps.Add(EmitJumpSite(chunk, OpCode.Jump)); // body terminator -> end

            // Next case starts here.
            PatchJumpSite(chunk, nextCaseSite);
        }

        if (defaultMarker is long d) EmitMarker(chunk, d);

        foreach (int site in endJumps) PatchJumpSite(chunk, site);
        chunk.WriteOpCode(OpCode.PopN, 1);
        chunk.WriteByte(1, 1); // discard subject
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, output) = NewVm();
        vm.Run(chunk);
        Assert.Equal(0, vm.Stack.Count);
        return output.ToString();
    }

    [Fact]
    public void Select_FirstMatchingCaseRuns_AndSelectExits() {
        // Two cases could be considered; only the first matching one runs.
        string output = RunSelect(
            subject: 1,
            cases: [new SelectCase([1], 100), new SelectCase([1], 200)],
            defaultMarker: 999);
        Assert.Equal($"100{Environment.NewLine}", output);
    }

    [Fact]
    public void Select_DefaultRuns_OnlyWhenNoCaseMatches() {
        string output = RunSelect(
            subject: 7,
            cases: [new SelectCase([1], 100), new SelectCase([2], 200)],
            defaultMarker: 999);
        Assert.Equal($"999{Environment.NewLine}", output);
    }

    [Fact]
    public void Select_NoMatchNoDefault_DoesNothing() {
        string output = RunSelect(
            subject: 7,
            cases: [new SelectCase([1], 100), new SelectCase([2], 200)],
            defaultMarker: null);
        Assert.Equal(string.Empty, output);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(2L)]
    public void Select_MultiValueCase_MatchesEitherValue(long subject) {
        string output = RunSelect(
            subject: subject,
            cases: [new SelectCase([1, 2], 100)],
            defaultMarker: 999);
        Assert.Equal($"100{Environment.NewLine}", output);
    }

    [Fact]
    public void Select_MultiValueCase_FallsToDefaultWhenNeitherMatches() {
        string output = RunSelect(
            subject: 3,
            cases: [new SelectCase([1, 2], 100)],
            defaultMarker: 999);
        Assert.Equal($"999{Environment.NewLine}", output);
    }

    // -------------------------------------------------------------------------
    // D-332: a growth-inducing run does not leak into the next run on the
    // same VM instance. The enlarged backing array persists (by design — the
    // capacity is not reset, only the live region is cleared by
    // ValueStack.Reset()) but a subsequent shallow run must behave identically
    // to a fresh VM. Regression tripwire against any future pooling approach.
    // -------------------------------------------------------------------------

    [Fact]
    public void Run_TwoSequentialRuns_FirstGrowsStack_SecondIsCorrectAndIsolated() {
        const int iterations = 1100;

        // Run 1: i := 0 (slot 0); while (i < 1100) { push a padding constant
        // (never popped) ; i++ }; push final i; (top-level) Return leaves
        // every pushed value on the stack — the top-level Return branch does
        // not trim.
        var run1 = new Chunk();
        int zero = run1.AddConstant(GrobValue.FromInt(0));
        int n = run1.AddConstant(GrobValue.FromInt(iterations));
        int padding = run1.AddConstant(GrobValue.FromInt(-1));

        run1.WriteOpCode(OpCode.Constant, 1); run1.WriteByte((byte)zero, 1);   // i := 0 (slot 0)

        int loopStart = run1.Count;
        run1.WriteOpCode(OpCode.GetLocal, 1); run1.WriteByte(0, 1);
        run1.WriteOpCode(OpCode.Constant, 1); run1.WriteByte((byte)n, 1);
        run1.WriteOpCode(OpCode.LessInt, 1);
        run1.WriteOpCode(OpCode.JumpIfFalse, 1);
        int loopExitSite = run1.Count;
        run1.WriteByte(0xFF, 1); run1.WriteByte(0xFF, 1);

        run1.WriteOpCode(OpCode.Constant, 1); run1.WriteByte((byte)padding, 1); // padding push
        run1.WriteOpCode(OpCode.IncrementInt, 1); run1.WriteByte(0, 1);         // i++
        run1.WriteOpCode(OpCode.Loop, 1);
        int loopOffset = (run1.Count + 2) - loopStart;
        run1.WriteByte((byte)(loopOffset >> 8), 1); run1.WriteByte((byte)(loopOffset & 0xFF), 1);

        int loopEnd = run1.Count;
        int exitOffset = loopEnd - (loopExitSite + 2);
        run1.PatchByte(loopExitSite, (byte)(exitOffset >> 8));
        run1.PatchByte(loopExitSite + 1, (byte)(exitOffset & 0xFF));

        run1.WriteOpCode(OpCode.GetLocal, 1); run1.WriteByte(0, 1);   // push final i
        run1.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(run1);
        Assert.Equal((long)iterations, vm.Stack.Peek().AsInt());

        // Run 2, same VM instance: a plain shallow script must produce the
        // same result it would on a fresh VM — no leakage from run 1's
        // leftover values or its now-enlarged backing array.
        var run2 = new Chunk();
        byte i2 = ConstByte(run2, GrobValue.FromInt(2));
        byte i3 = ConstByte(run2, GrobValue.FromInt(3));
        byte i4 = ConstByte(run2, GrobValue.FromInt(4));
        run2.WriteOpCode(OpCode.Constant, 1); run2.WriteByte(i2, 1);
        run2.WriteOpCode(OpCode.Constant, 1); run2.WriteByte(i3, 1);
        run2.WriteOpCode(OpCode.Constant, 1); run2.WriteByte(i4, 1);
        run2.WriteOpCode(OpCode.MultiplyInt, 1);
        run2.WriteOpCode(OpCode.AddInt, 1);
        run2.WriteOpCode(OpCode.Return, 1);

        vm.Run(run2);
        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(14L, vm.Stack.Peek().AsInt());
    }
}
