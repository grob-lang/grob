using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 7 Increment C — <c>finally</c>. All chunks
/// are hand-constructed; no compiler dependency (Grob.Vm.Tests never references
/// Grob.Compiler). Two groups:
/// <list type="bullet">
/// <item><description>Paths reachable through the pre-existing sequential dispatch
/// loop alone — normal try completion and normal catch completion — which the
/// try/catch normal-completion convergence-point emission (already built) already
/// satisfies with zero VM changes. These are sanity/regression coverage, not new
/// behaviour.</description></item>
/// <item><description>The exceptional path — <see cref="TryRegion.FinallyOffset"/>
/// consulted when an exception unwinds through a region — which has no VM support
/// yet (escalated to <c>grob-unwind-specialist</c>, sub-problem 2). These assert
/// the acceptance surface: exactly-once execution, throw-in-finally replacement,
/// and D-325 upvalue survival.</description></item>
/// </list>
/// </summary>
public sealed class VirtualMachineFinallyTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    /// <summary>Backpatches a 2-byte forward jump written as two 0xFF placeholder bytes.</summary>
    private static void PatchJump16(Chunk chunk, int patchSite) {
        int offset = chunk.Count - (patchSite + 2);
        chunk.PatchByte(patchSite, (byte)(offset >> 8));
        chunk.PatchByte(patchSite + 1, (byte)(offset & 0xFF));
    }

    /// <summary>Emits <c>globals[name] = globals[name] + 1</c>, so a test can assert
    /// "this finally ran exactly N times" via the final value. Reads via GetGlobal on
    /// an unset global would fault, so the counter global is always pre-seeded to 0 by
    /// the caller before Run() — SetGlobal itself also requires the global to already
    /// be defined (E1001 otherwise), and (like DefineGlobal) pops the value it stores
    /// without pushing anything back.</summary>
    private static void EmitIncrementGlobalCounter(Chunk chunk, int nameConstIdx, int line) {
        chunk.WriteOpCode(OpCode.GetGlobal, line); chunk.WriteByte((byte)nameConstIdx, line);
        int one = chunk.AddConstant(GrobValue.FromInt(1));
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte((byte)one, line);
        chunk.WriteOpCode(OpCode.AddInt, line);
        chunk.WriteOpCode(OpCode.SetGlobal, line); chunk.WriteByte((byte)nameConstIdx, line);
    }

    // -----------------------------------------------------------------------
    // Already-supported paths (sequential dispatch alone) — regression
    // coverage for the pre-existing convergence-point emission, exercised
    // end-to-end through the VM rather than only via compiler disassembly.
    // -----------------------------------------------------------------------

    [Fact]
    public void NormalCompletion_NoException_RunsFinallyOnce() {
        var script = new Chunk();
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count; // empty try body — falls straight through
        int endOffset = script.Count;

        int finallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, counterName, 3);

        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(1L, vm.Globals["ran"].AsInt());
    }

    [Fact]
    public void CaughtException_HandlerCompletesNormally_RunsFinallyOnce() {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)msgConst, 2);
        script.WriteOpCode(OpCode.NewStruct, 2); script.WriteByte(typeIdx, 2);
        script.WriteOpCode(OpCode.Throw, 2);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count; // empty catch body — binds at slot 0

        PatchJump16(script, jumpSite);
        int finallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, counterName, 3);

        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["IoError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)], finallyOffset));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(1L, vm.Globals["ran"].AsInt());
    }

    // -----------------------------------------------------------------------
    // The exceptional path — TryRegion.FinallyOffset consulted while an
    // exception unwinds. No VM support yet (grob-unwind-specialist,
    // sub-problem 2).
    // -----------------------------------------------------------------------

    [Fact]
    public void UncaughtThrow_PropagatingPastTry_RunsFinallyThenExceptionContinues() {
        // No catches at all — the throw is unhandled. The finally must still run
        // once before the exception reaches the top level.
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)msgConst, 2);
        script.WriteOpCode(OpCode.NewStruct, 2); script.WriteByte(typeIdx, 2);
        script.WriteOpCode(OpCode.Throw, 2);
        int endOffset = script.Count;

        int finallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, counterName, 3);

        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        // No handlers — nothing matches, so the throw is unhandled.
        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset));

        var (vm, output) = NewVm();
        Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(1L, vm.Globals["ran"].AsInt());
        _ = output;
    }

    [Fact]
    public void NestedTryFinally_UncaughtThrow_RunsBothFinallysInnerThenOuterExactlyOnce() {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int innerName = script.AddConstant(GrobValue.FromString("innerRan"));
        int outerName = script.AddConstant(GrobValue.FromString("outerRan"));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)innerName, 1);
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)outerName, 1);

        int outerRegionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)outerRegionIndex, 2);
        int outerStart = script.Count;

        int innerRegionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 3); script.WriteByte((byte)innerRegionIndex, 3);
        int innerStart = script.Count;

        script.WriteOpCode(OpCode.Constant, 4); script.WriteByte((byte)msgConst, 4);
        script.WriteOpCode(OpCode.NewStruct, 4); script.WriteByte(typeIdx, 4);
        script.WriteOpCode(OpCode.Throw, 4);
        int innerEnd = script.Count;

        int innerFinallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, innerName, 5);

        script.WriteOpCode(OpCode.TryEnd, 5);
        int outerEnd = script.Count;

        int outerFinallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, outerName, 6);

        script.WriteOpCode(OpCode.TryEnd, 6);
        script.WriteOpCode(OpCode.Return, 6);

        // Neither region has a catch — the throw is unhandled and unwinds
        // through both, each finally running exactly once, inner then outer.
        script.SetTryRegion(innerRegionIndex, new TryRegion(innerStart, innerEnd, [], innerFinallyOffset));
        script.SetTryRegion(outerRegionIndex, new TryRegion(outerStart, outerEnd, [], outerFinallyOffset));

        var (vm, _) = NewVm();
        Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(1L, vm.Globals["innerRan"].AsInt());
        Assert.Equal(1L, vm.Globals["outerRan"].AsInt());
    }

    [Fact]
    public void CaughtException_HandlerThrows_RunsFinallyThenNewExceptionPropagates() {
        // The catch handler itself throws a different exception type. The
        // enclosing try's own finally must still run once, and the NEW
        // exception (not the original IoError) is what propagates.
        var script = new Chunk();
        byte ioIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        byte netIdx = script.AddStructType(new StructTypeDescriptor("NetworkError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int msg2Const = script.AddConstant(GrobValue.FromString("handler failure"));
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)msgConst, 2);
        script.WriteOpCode(OpCode.NewStruct, 2); script.WriteByte(ioIdx, 2);
        script.WriteOpCode(OpCode.Throw, 2);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count;
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)msg2Const, 3);
        script.WriteOpCode(OpCode.NewStruct, 3); script.WriteByte(netIdx, 3);
        script.WriteOpCode(OpCode.Throw, 3);

        PatchJump16(script, jumpSite);
        int finallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, counterName, 4);

        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["IoError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)], finallyOffset));

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Contains("NetworkError", ex.Message);
        Assert.DoesNotContain("IoError", ex.Message);
        Assert.Equal(1L, vm.Globals["ran"].AsInt());
    }

    [Fact]
    public void ThrowInFinally_ReplacesInFlightException() {
        // The finally itself throws while the original IoError is in flight
        // (unhandled by this region). The NEW exception (NetworkError)
        // replaces it — an outer catch must see NetworkError, not IoError.
        var script = new Chunk();
        byte ioIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        byte netIdx = script.AddStructType(new StructTypeDescriptor("NetworkError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int msg2Const = script.AddConstant(GrobValue.FromString("finally failure"));
        int outerCaughtName = script.AddConstant(GrobValue.FromString("outerCaughtType"));

        int outerRegionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)outerRegionIndex, 1);
        int outerStart = script.Count;

        int innerRegionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)innerRegionIndex, 2);
        int innerStart = script.Count;

        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)msgConst, 3);
        script.WriteOpCode(OpCode.NewStruct, 3); script.WriteByte(ioIdx, 3);
        script.WriteOpCode(OpCode.Throw, 3);
        int innerEnd = script.Count;

        // The inner region's finally throws NetworkError instead of running to
        // completion — no catches on the inner region, so this finally runs on
        // the unhandled-propagation path.
        int innerFinallyOffset = script.Count;
        script.WriteOpCode(OpCode.Constant, 4); script.WriteByte((byte)msg2Const, 4);
        script.WriteOpCode(OpCode.NewStruct, 4); script.WriteByte(netIdx, 4);
        script.WriteOpCode(OpCode.Throw, 4);

        script.WriteOpCode(OpCode.TryEnd, 4);
        int outerEnd = script.Count;

        script.WriteOpCode(OpCode.Jump, 5);
        int outerJumpSite = script.Count;
        script.WriteByte(0xFF, 5); script.WriteByte(0xFF, 5);

        // Outer catch-all: records the caught value's type name.
        int outerHandlerOffset = script.Count;
        script.WriteOpCode(OpCode.GetLocal, 6); script.WriteByte(0, 6); // the bound exception
        script.WriteOpCode(OpCode.DefineGlobal, 6); script.WriteByte((byte)outerCaughtName, 6);

        PatchJump16(script, outerJumpSite);
        script.WriteOpCode(OpCode.TryEnd, 6);
        script.WriteOpCode(OpCode.Return, 6);

        script.SetTryRegion(innerRegionIndex, new TryRegion(innerStart, innerEnd, [], innerFinallyOffset));
        script.SetTryRegion(outerRegionIndex, new TryRegion(outerStart, outerEnd,
            [new CatchHandler([], IsCatchAll: true, outerHandlerOffset, BindingSlot: 0)]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.True(vm.Globals.TryGetValue("outerCaughtType", out GrobValue caught));
        Assert.True(caught.TryAsStruct(out GrobStruct? s));
        Assert.Equal("NetworkError", s!.TypeName);
    }

    [Fact]
    public void ClosureCapturedBeforeThrow_ReadAfterFinallyRuns_NoUnderflow() {
        // D-325: a closure captured before a throw, reached after the finally
        // (which itself runs on the unhandled-propagation path) runs, still
        // reads its captured value correctly — the upvalue closes by stack
        // location as frames tear down, unaffected by the finally running in
        // between.
        var lambdaChunk = new Chunk();
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        lambdaChunk.WriteOpCode(OpCode.Nil, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        var throwerChunk = new Chunk();
        int nineNineNine = throwerChunk.AddConstant(GrobValue.FromInt(999));
        int lambdaIdx = throwerChunk.AddConstant(GrobValue.FromFunction(lambdaFn));
        int globalName = throwerChunk.AddConstant(GrobValue.FromString("capturedClosure"));
        int msgConst = throwerChunk.AddConstant(GrobValue.FromString("boom"));
        byte typeIdx = throwerChunk.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int ranName = throwerChunk.AddConstant(GrobValue.FromString("finallyRan"));

        throwerChunk.WriteOpCode(OpCode.Constant, 1); throwerChunk.WriteByte((byte)nineNineNine, 1);
        throwerChunk.WriteOpCode(OpCode.Closure, 1); throwerChunk.WriteByte((byte)lambdaIdx, 1);
        throwerChunk.WriteByte(1, 1); throwerChunk.WriteByte(0, 1);
        throwerChunk.WriteOpCode(OpCode.DefineGlobal, 1); throwerChunk.WriteByte((byte)globalName, 1);

        int regionIndex = throwerChunk.AddTryRegion();
        throwerChunk.WriteOpCode(OpCode.TryBegin, 2); throwerChunk.WriteByte((byte)regionIndex, 2);
        int startOffset = throwerChunk.Count;

        throwerChunk.WriteOpCode(OpCode.Constant, 2); throwerChunk.WriteByte((byte)msgConst, 2);
        throwerChunk.WriteOpCode(OpCode.NewStruct, 2); throwerChunk.WriteByte(typeIdx, 2);
        throwerChunk.WriteOpCode(OpCode.Throw, 2);
        int endOffset = throwerChunk.Count;

        int finallyOffset = throwerChunk.Count;
        EmitIncrementGlobalCounter(throwerChunk, ranName, 3);

        throwerChunk.WriteOpCode(OpCode.TryEnd, 4);
        throwerChunk.WriteOpCode(OpCode.Nil, 4);
        throwerChunk.WriteOpCode(OpCode.Return, 4);
        var throwerFn = new BytecodeFunction("thrower", 0, throwerChunk);

        var script = new Chunk();
        int fnIdx = script.AddConstant(GrobValue.FromFunction(throwerFn));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        // A separate constant pool from throwerChunk's — 'ranName' there is not a
        // valid index here, even though the underlying global name string matches.
        int ranNameInScript = script.AddConstant(GrobValue.FromString("finallyRan"));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)ranNameInScript, 1);

        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)fnIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Pop, 1);
        script.WriteOpCode(OpCode.Return, 1);

        throwerChunk.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset));

        var (vm, _) = NewVm();
        Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(1L, vm.Globals["finallyRan"].AsInt());

        var readBack = new Chunk();
        int nameConst = readBack.AddConstant(GrobValue.FromString("capturedClosure"));
        readBack.WriteOpCode(OpCode.GetGlobal, 1); readBack.WriteByte((byte)nameConst, 1);
        readBack.WriteOpCode(OpCode.Call, 1); readBack.WriteByte(0, 1);
        readBack.WriteOpCode(OpCode.Return, 1);

        vm.Run(readBack);
        Assert.Equal(999L, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void ExitInsideTryFinally_RunsNoFinally_TerminatesWithExitCode() {
        // Already correct by construction — ExitSignal (D-110) unwinds the .NET
        // call stack past the bytecode dispatch loop entirely, so it never
        // reaches the region walk that would consult FinallyOffset. Regression
        // guard, not new behaviour.
        var script = new Chunk();
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;

        int five = script.AddConstant(GrobValue.FromInt(5));
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)five, 2);
        script.WriteOpCode(OpCode.Exit, 2);
        int endOffset = script.Count;

        int finallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, counterName, 3);

        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset));

        var (vm, _) = NewVm();
        GrobExitException ex = Assert.Throws<GrobExitException>(() => vm.Run(script));

        Assert.Equal(5, ex.Code);
        Assert.False(vm.Globals.TryGetValue("ran", out GrobValue ranValue) && ranValue.AsInt() == 1);
    }

    // -----------------------------------------------------------------------
    // Layer invariant — pathological but well-formed finally-bearing regions
    // never crash the VM with a host exception.
    // -----------------------------------------------------------------------

    [Fact]
    public void Throw_RegionHasFinallyButNoHandlers_NeverThrowsHostException() {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)msgConst, 1);
        script.WriteOpCode(OpCode.NewStruct, 1); script.WriteByte(typeIdx, 1);
        script.WriteOpCode(OpCode.Throw, 1);
        int endOffset = script.Count;

        int finallyOffset = script.Count;
        script.WriteOpCode(OpCode.Nil, 2);
        script.WriteOpCode(OpCode.Pop, 2);

        script.WriteOpCode(OpCode.TryEnd, 2);
        script.WriteOpCode(OpCode.Return, 2);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset));

        var (vm, _) = NewVm();
        Exception? ex = Record.Exception(() => vm.Run(script));
        Assert.IsType<GrobRuntimeException>(ex);
    }
}
