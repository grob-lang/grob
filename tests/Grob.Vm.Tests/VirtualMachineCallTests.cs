using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 5 Increment A — the <see cref="OpCode.Call"/>
/// and <see cref="OpCode.Return"/> handlers, the call-frame array and the E5901
/// stack-overflow guard. All chunks are hand-constructed; no compiler dependency.
/// </summary>
/// <remarks>
/// The call convention under test: the caller pushes the callee
/// (<see cref="BytecodeFunction"/>) then its arguments, then <c>Call argCount</c>.
/// The arguments become the callee's first locals (slot 0 = first parameter)
/// addressed against the new frame's stack base. <c>Return</c> pops the result,
/// discards the callee value, its arguments and its locals, then pushes the
/// result — so a call replaces "callee + args" on the stack with a single value.
/// </remarks>
public sealed class VirtualMachineCallTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    // Jump helpers mirroring the compiler's EmitJump/PatchJump so hand-built
    // function bodies can branch with the same 2-byte big-endian offset shape
    // the VM decodes (offset counts from the byte after the two operand bytes).
    private static int EmitJump(Chunk chunk, OpCode op) {
        chunk.WriteOpCode(op, 1);
        int site = chunk.Count;
        chunk.WriteByte(0xFF, 1);
        chunk.WriteByte(0xFF, 1);
        return site;
    }

    private static void PatchJump(Chunk chunk, int site) {
        int offset = chunk.Count - (site + 2);
        chunk.PatchByte(site, (byte)(offset >> 8));
        chunk.PatchByte(site + 1, (byte)(offset & 0xFF));
    }

    // -----------------------------------------------------------------------
    // A simple positional call returns the right value and leaves the stack at
    // the expected depth.
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_PositionalArgument_ReturnsComputedValue() {
        // fn body: return arg0 + 100
        var fnChunk = new Chunk();
        int hundred = fnChunk.AddConstant(GrobValue.FromInt(100));
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1); // slot 0 = first parameter
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)hundred, 1);
        fnChunk.WriteOpCode(OpCode.AddInt, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        var fn = new BytecodeFunction("addHundred", 1, fnChunk);

        // script: addHundred(5)  → leaves 105 on the stack
        var script = new Chunk();
        int fnConst = script.AddConstant(GrobValue.FromFunction(fn));
        int five = script.AddConstant(GrobValue.FromInt(5));
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)fnConst, 1);
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)five, 1);
        script.WriteOpCode(OpCode.Call, 1);
        script.WriteByte(1, 1); // arg count
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(105L, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Call_AfterReturn_DiscardsCalleeValueAndArguments() {
        // fn body: return 42 (ignores its argument)
        var fnChunk = new Chunk();
        int answer = fnChunk.AddConstant(GrobValue.FromInt(42));
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)answer, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        var fn = new BytecodeFunction("answer", 1, fnChunk);

        var script = new Chunk();
        int fnConst = script.AddConstant(GrobValue.FromFunction(fn));
        int arg = script.AddConstant(GrobValue.FromInt(7));
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)fnConst, 1);
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)arg, 1);
        script.WriteOpCode(OpCode.Call, 1);
        script.WriteByte(1, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        // The callee value and its argument are gone; only the return value remains.
        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // A directly recursive function computes correctly (factorial).
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_DirectRecursion_ComputesFactorial() {
        // fn factorial(n): if n <= 1 { return 1 } return n * factorial(n - 1)
        var fnChunk = new Chunk();
        int one = fnChunk.AddConstant(GrobValue.FromInt(1));
        int selfName = fnChunk.AddConstant(GrobValue.FromString("factorial"));

        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);                 // n
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)one, 1);
        fnChunk.WriteOpCode(OpCode.LessEqualInt, 1); // n <= 1
        int elseJump = EmitJump(fnChunk, OpCode.JumpIfFalse);

        // base case: return 1
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)one, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);

        // recursive case: return n * factorial(n - 1)
        PatchJump(fnChunk, elseJump);
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);                 // n (left operand of *)
        fnChunk.WriteOpCode(OpCode.GetGlobal, 1);
        fnChunk.WriteByte((byte)selfName, 1);    // factorial
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);                 // n
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)one, 1);
        fnChunk.WriteOpCode(OpCode.SubtractInt, 1); // n - 1
        fnChunk.WriteOpCode(OpCode.Call, 1);
        fnChunk.WriteByte(1, 1);                 // factorial(n - 1)
        fnChunk.WriteOpCode(OpCode.MultiplyInt, 1); // n * factorial(n - 1)
        fnChunk.WriteOpCode(OpCode.Return, 1);

        var fn = new BytecodeFunction("factorial", 1, fnChunk);

        var script = new Chunk();
        int fnConst = script.AddConstant(GrobValue.FromFunction(fn));
        int name = script.AddConstant(GrobValue.FromString("factorial"));
        int five = script.AddConstant(GrobValue.FromInt(5));
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)fnConst, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1);
        script.WriteByte((byte)name, 1);
        script.WriteOpCode(OpCode.GetGlobal, 1);
        script.WriteByte((byte)name, 1);
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)five, 1);
        script.WriteOpCode(OpCode.Call, 1);
        script.WriteByte(1, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(120L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Non-terminating recursion raises E5901 — a clean RuntimeError, not a host
    // CLR StackOverflowException (which would crash the process before any
    // assertion could run).
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_UnboundedRecursion_RaisesE5901() {
        // fn rec(): return rec()   — never terminates
        var fnChunk = new Chunk();
        int selfName = fnChunk.AddConstant(GrobValue.FromString("rec"));
        fnChunk.WriteOpCode(OpCode.GetGlobal, 1);
        fnChunk.WriteByte((byte)selfName, 1);
        fnChunk.WriteOpCode(OpCode.Call, 1);
        fnChunk.WriteByte(0, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        var fn = new BytecodeFunction("rec", 0, fnChunk);

        var script = new Chunk();
        int fnConst = script.AddConstant(GrobValue.FromFunction(fn));
        int name = script.AddConstant(GrobValue.FromString("rec"));
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)fnConst, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1);
        script.WriteByte((byte)name, 1);
        script.WriteOpCode(OpCode.GetGlobal, 1);
        script.WriteByte((byte)name, 1);
        script.WriteOpCode(OpCode.Call, 1);
        script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));
        Assert.Equal(ErrorCatalog.E5901.Code, ex.Code);
        Assert.Equal(1, ex.Line);    // the recursive call site's line in the hand-built chunk
        Assert.Equal(0, ex.Column);  // no column recorded for hand-built bytecode
    }

    // -----------------------------------------------------------------------
    // D-332: recursion deep enough to force a value-stack resize still
    // computes correctly — GetLocal/SetLocal and arithmetic read the right
    // slots through the backing-array grow-and-copy.
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>fn sumDown(n): if n &lt;= 0 { return 0 } else { return n + sumDown(n - 1) }</c>,
    /// with five padding constants pushed (and never explicitly popped — the
    /// frame's own <see cref="OpCode.Return"/> trims them) ahead of the recursive
    /// call in the else branch, so live operand-stack depth grows by roughly eight
    /// slots per recursion level. At <paramref name="depth"/> = 200 (well under the
    /// 256-deep call-frame cap, D-180) this comfortably crosses
    /// <see cref="ValueStack.DefaultCapacity"/> before any frame returns, forcing at
    /// least one backing-array resize mid-recursion.
    /// </summary>
    [Fact]
    public void Call_DeepRecursionAcrossStackGrowth_ProducesCorrectResult() {
        const int depth = 200;

        var fnChunk = new Chunk();
        int zero = fnChunk.AddConstant(GrobValue.FromInt(0));
        int one = fnChunk.AddConstant(GrobValue.FromInt(1));
        int padding = fnChunk.AddConstant(GrobValue.FromInt(-1));
        int selfName = fnChunk.AddConstant(GrobValue.FromString("sumDown"));

        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);                     // n
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)zero, 1);
        fnChunk.WriteOpCode(OpCode.LessEqualInt, 1); // n <= 0
        int elseJump = EmitJump(fnChunk, OpCode.JumpIfFalse);

        // base case: return 0
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)zero, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);

        // recursive case: five padding pushes, then n + sumDown(n - 1)
        PatchJump(fnChunk, elseJump);
        for (int i = 0; i < 5; i++) {
            fnChunk.WriteOpCode(OpCode.Constant, 1);
            fnChunk.WriteByte((byte)padding, 1);
        }
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);                     // n (left operand of +)
        fnChunk.WriteOpCode(OpCode.GetGlobal, 1);
        fnChunk.WriteByte((byte)selfName, 1);         // sumDown
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);                     // n
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)one, 1);
        fnChunk.WriteOpCode(OpCode.SubtractInt, 1);   // n - 1
        fnChunk.WriteOpCode(OpCode.Call, 1);
        fnChunk.WriteByte(1, 1);                     // sumDown(n - 1)
        fnChunk.WriteOpCode(OpCode.AddInt, 1);        // n + sumDown(n - 1)
        fnChunk.WriteOpCode(OpCode.Return, 1);

        var fn = new BytecodeFunction("sumDown", 1, fnChunk);

        var script = new Chunk();
        int fnConst = script.AddConstant(GrobValue.FromFunction(fn));
        int name = script.AddConstant(GrobValue.FromString("sumDown"));
        int start = script.AddConstant(GrobValue.FromInt(depth));
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)fnConst, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1);
        script.WriteByte((byte)name, 1);
        script.WriteOpCode(OpCode.GetGlobal, 1);
        script.WriteByte((byte)name, 1);
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)start, 1);
        script.WriteOpCode(OpCode.Call, 1);
        script.WriteByte(1, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal((long)depth * (depth + 1) / 2, vm.Stack.Peek().AsInt());
    }
}
