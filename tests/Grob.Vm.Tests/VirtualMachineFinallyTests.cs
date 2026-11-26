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

        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));

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

        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["IoError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));

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

        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        // No handlers — nothing matches, so the throw is unhandled.
        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));

        var (vm, output) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(2, ex.Line);
        Assert.Equal(0, ex.Column);
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

        int innerTryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 5);
        int outerEnd = script.Count;

        int outerFinallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, outerName, 6);

        int outerTryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 6);
        script.WriteOpCode(OpCode.Return, 6);

        // Neither region has a catch — the throw is unhandled and unwinds
        // through both, each finally running exactly once, inner then outer.
        script.SetTryRegion(innerRegionIndex, new TryRegion(innerStart, innerEnd, [], innerFinallyOffset, BaseSlot: 0,
            TryEndOffset: innerTryEnd));
        script.SetTryRegion(outerRegionIndex, new TryRegion(outerStart, outerEnd, [], outerFinallyOffset, BaseSlot: 0,
            TryEndOffset: outerTryEnd));

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(4, ex.Line);
        Assert.Equal(0, ex.Column);
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

        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["IoError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(3, ex.Line);
        Assert.Equal(0, ex.Column);
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

        int innerTryEnd = script.Count;
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
        int outerTryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 6);
        script.WriteOpCode(OpCode.Return, 6);

        script.SetTryRegion(innerRegionIndex, new TryRegion(innerStart, innerEnd, [], innerFinallyOffset, BaseSlot: 0,
            TryEndOffset: innerTryEnd));
        script.SetTryRegion(outerRegionIndex, new TryRegion(outerStart, outerEnd,
            [new CatchHandler([], IsCatchAll: true, outerHandlerOffset, BindingSlot: 0)], FinallyOffset: -1, BaseSlot: 0,
            TryEndOffset: outerTryEnd));

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

        int tryEndOffset = throwerChunk.Count;
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

        // The captured local (999) holds slot 0 below the try, so the region's base is 1.
        throwerChunk.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset, BaseSlot: 1, TryEndOffset: tryEndOffset));

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(2, ex.Line);
        Assert.Equal(0, ex.Column);
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

        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 4);
        script.WriteOpCode(OpCode.Return, 4);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));

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

        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 2);
        script.WriteOpCode(OpCode.Return, 2);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));

        var (vm, _) = NewVm();
        Exception? ex = Record.Exception(() => vm.Run(script));
        Assert.IsType<GrobRuntimeException>(ex);
    }

    // -----------------------------------------------------------------------
    // R-79 (D-455): a finally that a throw runs finds the stack trimmed to its
    // region's BaseSlot, as the normal path leaves it, so its first local sits
    // where the compiler put it — whatever the throw left above that slot.
    // -----------------------------------------------------------------------

    private static void EmitConstant(Chunk chunk, GrobValue value, int line) {
        int index = chunk.AddConstant(value);
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte((byte)index, line);
    }

    private static void EmitThrow(Chunk chunk, byte typeIdx, string message, int line) {
        EmitConstant(chunk, GrobValue.FromString(message), line);
        chunk.WriteOpCode(OpCode.NewStruct, line); chunk.WriteByte(typeIdx, line);
        chunk.WriteOpCode(OpCode.Throw, line);
    }

    private static int SeedGlobal(Chunk chunk, string name, int line = 1) {
        int nameIdx = chunk.AddConstant(GrobValue.FromString(name));
        chunk.WriteOpCode(OpCode.Nil, line);
        chunk.WriteOpCode(OpCode.DefineGlobal, line); chunk.WriteByte((byte)nameIdx, line);
        return nameIdx;
    }

    private static void EmitLocalToGlobal(Chunk chunk, int slot, int nameIdx, int line) {
        chunk.WriteOpCode(OpCode.GetLocal, line); chunk.WriteByte((byte)slot, line);
        chunk.WriteOpCode(OpCode.SetGlobal, line); chunk.WriteByte((byte)nameIdx, line);
    }

    [Fact]
    public void FinallyOnThrow_LocalDeclaredInTryBody_FinallyReadsItsOwnLocal() {
        // try { a := true; throw } finally { b := 1; out = b + 1 }
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int outName = SeedGlobal(script, "out");

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;
        EmitConstant(script, GrobValue.FromBool(true), 3); // a, slot 0
        EmitThrow(script, typeIdx, "boom", 4);
        int endOffset = script.Count;

        int finallyOffset = script.Count;
        EmitConstant(script, GrobValue.FromInt(1), 6);     // b, slot 0
        script.WriteOpCode(OpCode.GetLocal, 7); script.WriteByte(0, 7);
        EmitConstant(script, GrobValue.FromInt(1), 7);
        script.WriteOpCode(OpCode.AddInt, 7);
        script.WriteOpCode(OpCode.SetGlobal, 7); script.WriteByte((byte)outName, 7);
        script.WriteOpCode(OpCode.Pop, 8);
        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 8);
        script.WriteOpCode(OpCode.Return, 8);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(4, ex.Line);
        Assert.Equal(0, ex.Column);
        Assert.Equal(2L, vm.Globals["out"].AsInt());
    }

    [Fact]
    public void FinallyOnThrow_LocalDeclaredInCatchBodyThatThrows_FinallyReadsItsOwnLocal() {
        // try { throw } catch e { c := "c"; throw } finally { b := "f"; out = b }
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int outName = SeedGlobal(script, "out");

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;
        EmitThrow(script, typeIdx, "first", 3);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 3);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 3); script.WriteByte(0xFF, 3);

        int handlerOffset = script.Count;                   // e, slot 0
        EmitConstant(script, GrobValue.FromString("c"), 5); // c, slot 1
        EmitThrow(script, typeIdx, "second", 6);

        PatchJump16(script, jumpSite);
        int finallyOffset = script.Count;
        EmitConstant(script, GrobValue.FromString("f"), 8); // b, slot 0
        EmitLocalToGlobal(script, 0, outName, 9);
        script.WriteOpCode(OpCode.Pop, 10);
        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 10);
        script.WriteOpCode(OpCode.Return, 10);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler([], IsCatchAll: true, handlerOffset, BindingSlot: 0)], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(6, ex.Line);
        Assert.Equal(0, ex.Column);
        Assert.Equal("f", vm.Globals["out"].AsString());
    }

    [Fact]
    public void FinallyOnThrow_NestedTry_EachFinallyTrimsToItsOwnBase() {
        // try { x := 10; try { y := 20; throw } finally { z := 30; inner = z; seenX = x } }
        // finally { w := 40; outer = w }
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int innerName = SeedGlobal(script, "inner");
        int seenXName = SeedGlobal(script, "seenX");
        int outerName = SeedGlobal(script, "outer");

        int outerIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)outerIndex, 2);
        int outerStart = script.Count;
        EmitConstant(script, GrobValue.FromInt(10), 3);     // x, slot 0

        int innerIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 4); script.WriteByte((byte)innerIndex, 4);
        int innerStart = script.Count;
        EmitConstant(script, GrobValue.FromInt(20), 5);     // y, slot 1
        EmitThrow(script, typeIdx, "boom", 6);
        int innerEnd = script.Count;

        int innerFinally = script.Count;
        EmitConstant(script, GrobValue.FromInt(30), 8);     // z, slot 1
        EmitLocalToGlobal(script, 1, innerName, 9);
        EmitLocalToGlobal(script, 0, seenXName, 9);
        script.WriteOpCode(OpCode.Pop, 10);
        int innerTryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 10);

        script.WriteOpCode(OpCode.Pop, 11);                 // x's normal-path cleanup
        int outerEnd = script.Count;

        int outerFinally = script.Count;
        EmitConstant(script, GrobValue.FromInt(40), 13);    // w, slot 0
        EmitLocalToGlobal(script, 0, outerName, 14);
        script.WriteOpCode(OpCode.Pop, 15);
        int outerTryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 15);
        script.WriteOpCode(OpCode.Return, 15);

        script.SetTryRegion(innerIndex, new TryRegion(innerStart, innerEnd, [], innerFinally, BaseSlot: 1,
            TryEndOffset: innerTryEnd));
        script.SetTryRegion(outerIndex, new TryRegion(outerStart, outerEnd, [], outerFinally, BaseSlot: 0,
            TryEndOffset: outerTryEnd));

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(6, ex.Line);
        Assert.Equal(0, ex.Column);
        Assert.Equal(30L, vm.Globals["inner"].AsInt());
        Assert.Equal(10L, vm.Globals["seenX"].AsInt());
        Assert.Equal(40L, vm.Globals["outer"].AsInt());
    }

    [Fact]
    public void FinallyOnThrow_ThrowFromCalleeFrame_FinallyReadsItsOwnLocal() {
        // fn f() { c := 7; throw }   try { f() } finally { b := 5; out = b }
        var fChunk = new Chunk();
        byte fTypeIdx = fChunk.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        EmitConstant(fChunk, GrobValue.FromInt(7), 1);      // c, the callee's slot 0
        EmitThrow(fChunk, fTypeIdx, "boom", 2);
        fChunk.WriteOpCode(OpCode.Nil, 3);
        fChunk.WriteOpCode(OpCode.Return, 3);
        var fFn = new BytecodeFunction("f", 0, fChunk);

        var script = new Chunk();
        int outName = SeedGlobal(script, "out");
        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 5); script.WriteByte((byte)regionIndex, 5);
        int startOffset = script.Count;
        EmitConstant(script, GrobValue.FromFunction(fFn), 6);
        script.WriteOpCode(OpCode.Call, 6); script.WriteByte(0, 6);
        script.WriteOpCode(OpCode.Pop, 6);
        int endOffset = script.Count;

        int finallyOffset = script.Count;
        EmitConstant(script, GrobValue.FromInt(5), 8);      // b, slot 0
        EmitLocalToGlobal(script, 0, outName, 9);
        script.WriteOpCode(OpCode.Pop, 10);
        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 10);
        script.WriteOpCode(OpCode.Return, 10);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(2, ex.Line);
        Assert.Equal(0, ex.Column);
        Assert.Equal(5L, vm.Globals["out"].AsInt());
    }

    // -----------------------------------------------------------------------
    // R-80 (D-457): a finally that a throw runs ends at its own region's
    // closing TryEnd, at its own frame — however control left a try nested
    // inside it. A throw caught by an outer region, a jump out of a try body
    // (break, continue) and a callee's return from inside its own try each
    // skip that nested try's TryEnd. Each chunk ends by setting "overran",
    // which only a finally run past its end can reach: the first throw is
    // still in flight when the finally ends.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Emits <c>try { throw RuntimeError } finally { …body… }</c> at lines 2 to 13, then
    /// <c>overran = true</c> and the script's return at line 14.
    /// </summary>
    private static Chunk BuildThrowRunFinally(Action<Chunk> emitFinallyBody) {
        var script = new Chunk();
        byte runtimeIdx = script.AddStructType(new StructTypeDescriptor("RuntimeError", ["message"]));
        int overranName = SeedGlobal(script, "overran");

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;
        EmitThrow(script, runtimeIdx, "first", 2);
        int endOffset = script.Count;

        int finallyOffset = script.Count;
        emitFinallyBody(script);
        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 13);

        EmitConstant(script, GrobValue.FromBool(true), 14);
        script.WriteOpCode(OpCode.SetGlobal, 14); script.WriteByte((byte)overranName, 14);
        script.WriteOpCode(OpCode.Return, 14);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEndOffset));
        return script;
    }

    /// <summary>
    /// Runs a chunk from <see cref="BuildThrowRunFinally"/> and asserts the first error
    /// surfaces as E5904 at line 2, with the finally ended at its own closing TryEnd.
    /// </summary>
    private static VirtualMachine RunExpectingFirstError(Chunk script) {
        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(2, ex.Line);
        Assert.Equal(0, ex.Column);
        Assert.Equal("RuntimeError: first", ex.Message);
        Assert.True(vm.Globals["overran"].IsNil, "the finally ran past its own closing TryEnd");
        return vm;
    }

    /// <summary>
    /// Emits <c>try { …body… } catch (x: IoError) { }</c>: a typed catch a
    /// <c>LookupError</c> does not match, binding at slot 0.
    /// </summary>
    private static void EmitTryWithIoErrorCatch(Chunk chunk, Action emitBody, int line) {
        int regionIndex = chunk.AddTryRegion();
        chunk.WriteOpCode(OpCode.TryBegin, line); chunk.WriteByte((byte)regionIndex, line);
        int startOffset = chunk.Count;
        emitBody();
        int endOffset = chunk.Count;
        chunk.WriteOpCode(OpCode.Jump, line);
        int jumpSite = chunk.Count;
        chunk.WriteByte(0xFF, line); chunk.WriteByte(0xFF, line);
        int handlerOffset = chunk.Count;
        chunk.WriteOpCode(OpCode.Pop, line);                 // x's scope cleanup
        PatchJump16(chunk, jumpSite);
        int tryEndOffset = chunk.Count;
        chunk.WriteOpCode(OpCode.TryEnd, line);
        chunk.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["IoError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)], FinallyOffset: -1, BaseSlot: 0, TryEndOffset: tryEndOffset));
    }

    /// <summary>
    /// Emits <c>try { …body… } catch y { caught = y }</c>, binding at slot 0.
    /// </summary>
    private static void EmitTryWithCatchAllToGlobal(Chunk chunk, Action emitBody, int caughtName, int line) {
        int regionIndex = chunk.AddTryRegion();
        chunk.WriteOpCode(OpCode.TryBegin, line); chunk.WriteByte((byte)regionIndex, line);
        int startOffset = chunk.Count;
        emitBody();
        int endOffset = chunk.Count;
        chunk.WriteOpCode(OpCode.Jump, line);
        int jumpSite = chunk.Count;
        chunk.WriteByte(0xFF, line); chunk.WriteByte(0xFF, line);
        int handlerOffset = chunk.Count;
        EmitLocalToGlobal(chunk, 0, caughtName, line);
        chunk.WriteOpCode(OpCode.Pop, line);                 // y's scope cleanup
        PatchJump16(chunk, jumpSite);
        int tryEndOffset = chunk.Count;
        chunk.WriteOpCode(OpCode.TryEnd, line);
        chunk.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler([], IsCatchAll: true, handlerOffset, BindingSlot: 0)], FinallyOffset: -1, BaseSlot: 0, TryEndOffset: tryEndOffset));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FinallyOnThrow_TypedCatchesMissInsideIt_OuterCatchRunsAndTheFinallyEndsAtItsOwnTryEnd(int typedMisses) {
        // try { throw RuntimeError } finally {
        //     try { <typedMisses tries with catch (x: IoError)> { throw LookupError } } catch y { caught = y }
        // }
        // 0 is the LookupError caught where it is thrown; 1 is R-80's reproducer.
        int caughtName = -1;
        Chunk script = BuildThrowRunFinally(chunk => {
            byte lookupIdx = chunk.AddStructType(new StructTypeDescriptor("LookupError", ["message"]));
            caughtName = SeedGlobal(chunk, "caught", 3);
            void EmitTypedTries(int remaining) {
                if (remaining == 0) {
                    EmitThrow(chunk, lookupIdx, "second", 6);
                    return;
                }
                EmitTryWithIoErrorCatch(chunk, () => EmitTypedTries(remaining - 1), 5);
            }
            EmitTryWithCatchAllToGlobal(chunk, () => EmitTypedTries(typedMisses), caughtName, 4);
        });

        VirtualMachine vm = RunExpectingFirstError(script);

        Assert.True(vm.Globals["caught"].TryAsStruct(out GrobStruct? caught));
        Assert.Equal("LookupError", caught!.TypeName);
    }

    [Fact]
    public void FinallyOnThrow_JumpOutOfANestedTryBody_FinallyRunsOnAndEndsAtItsOwnTryEnd() {
        // try { throw RuntimeError } finally {
        //     try { break } catch e { }        — the jump lands past the nested TryEnd
        //     done = true
        // }
        Chunk script = BuildThrowRunFinally(chunk => {
            int doneName = SeedGlobal(chunk, "done", 3);
            int breakSite = -1;
            EmitTryWithIoErrorCatch(chunk, () => {
                chunk.WriteOpCode(OpCode.Jump, 5);
                breakSite = chunk.Count;
                chunk.WriteByte(0xFF, 5); chunk.WriteByte(0xFF, 5);
            }, 5);
            PatchJump16(chunk, breakSite);
            EmitConstant(chunk, GrobValue.FromBool(true), 7);
            chunk.WriteOpCode(OpCode.SetGlobal, 7); chunk.WriteByte((byte)doneName, 7);
        });

        VirtualMachine vm = RunExpectingFirstError(script);

        Assert.True(vm.Globals["done"].AsBool());
    }

    [Fact]
    public void FinallyOnThrow_CalleeReturnsFromInsideItsOwnTry_FinallyEndsAtItsOwnTryEnd() {
        // fn g(): int { try { return 1 } catch (x: IoError) { } }
        // try { throw RuntimeError } finally { got = g() }
        var gChunk = new Chunk();
        EmitTryWithIoErrorCatch(gChunk, () => {
            EmitConstant(gChunk, GrobValue.FromInt(1), 21);
            gChunk.WriteOpCode(OpCode.Return, 21);
        }, 20);
        gChunk.WriteOpCode(OpCode.Nil, 22);
        gChunk.WriteOpCode(OpCode.Return, 22);
        var gFn = new BytecodeFunction("g", 0, gChunk);

        Chunk script = BuildThrowRunFinally(chunk => {
            int gotName = SeedGlobal(chunk, "got", 3);
            EmitConstant(chunk, GrobValue.FromFunction(gFn), 4);
            chunk.WriteOpCode(OpCode.Call, 4); chunk.WriteByte(0, 4);
            chunk.WriteOpCode(OpCode.SetGlobal, 4); chunk.WriteByte((byte)gotName, 4);
        });

        VirtualMachine vm = RunExpectingFirstError(script);

        Assert.Equal(1L, vm.Globals["got"].AsInt());
    }

    [Fact]
    public void FinallyOnThrow_NestedTryFinallyCaughtFurtherOut_BothFinallysEndAtTheirOwnTryEnds() {
        // try { throw RuntimeError } finally {
        //     try { try { throw LookupError } finally { innerRan = true } } catch y { caught = y }
        // }
        Chunk script = BuildThrowRunFinally(chunk => {
            byte lookupIdx = chunk.AddStructType(new StructTypeDescriptor("LookupError", ["message"]));
            int caughtName = SeedGlobal(chunk, "caught", 3);
            int innerRanName = SeedGlobal(chunk, "innerRan", 3);
            EmitTryWithCatchAllToGlobal(chunk, () => {
                int innerIndex = chunk.AddTryRegion();
                chunk.WriteOpCode(OpCode.TryBegin, 5); chunk.WriteByte((byte)innerIndex, 5);
                int innerStart = chunk.Count;
                EmitThrow(chunk, lookupIdx, "second", 6);
                int innerEnd = chunk.Count;
                int innerFinally = chunk.Count;
                EmitConstant(chunk, GrobValue.FromBool(true), 8);
                chunk.WriteOpCode(OpCode.SetGlobal, 8); chunk.WriteByte((byte)innerRanName, 8);
                int tryEndOffset = chunk.Count;
                chunk.WriteOpCode(OpCode.TryEnd, 9);
                chunk.SetTryRegion(innerIndex, new TryRegion(innerStart, innerEnd, [], innerFinally, BaseSlot: 0, TryEndOffset: tryEndOffset));
            }, caughtName, 4);
        });

        VirtualMachine vm = RunExpectingFirstError(script);

        Assert.True(vm.Globals["innerRan"].AsBool());
        Assert.True(vm.Globals["caught"].TryAsStruct(out GrobStruct? caught));
        Assert.Equal("LookupError", caught!.TypeName);
    }
}
