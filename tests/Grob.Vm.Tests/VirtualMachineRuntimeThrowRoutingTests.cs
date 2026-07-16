using System.Runtime.ExceptionServices;
using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 7 Increment D — routing the Sprint 3-6
/// runtime-error sites (arithmetic, nil dereference, index/substring bounds,
/// stack-depth-exceeded) through the Sprint 7 A/B/C exception machinery so
/// <c>try</c>/<c>catch</c> catches them and <c>finally</c> runs around them.
/// All chunks are hand-constructed; no compiler dependency (Grob.Vm.Tests never
/// references Grob.Compiler). The pre-existing gold-master tests for each site's
/// <em>unhandled</em> behaviour (in <see cref="VirtualMachineTests"/>,
/// <see cref="VirtualMachineCallTests"/>, <see cref="VirtualMachineForInTests"/>)
/// are left unmodified and still pass — that unmodified pass is the "same
/// diagnostic when unhandled" proof; this file covers the newly-catchable half.
/// </summary>
public sealed class VirtualMachineRuntimeThrowRoutingTests {
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

    /// <summary>
    /// Runs <paramref name="action"/> on a dedicated thread with a large stack.
    /// The InvokeCallable re-entrant bridge costs several real C# stack frames
    /// per Grob call-depth level (RunDispatch → InvokeCallable → the invoker
    /// delegate → the native → its callback delegate) — several times more than
    /// a plain <see cref="OpCode.Call"/> level — so reaching the full 256-frame
    /// <c>MaxFrames</c> depth via this path can overflow the default 1&#160;MB
    /// thread stack before the VM's own guard ever fires. A larger stack here
    /// tests the VM's guard in isolation without that host-thread artefact.
    /// </summary>
    private static void RunOnLargeStackThread(Action action) {
        ExceptionDispatchInfo? captured = null;
        var thread = new Thread(() => {
            try {
                action();
            } catch (Exception ex) {
                captured = ExceptionDispatchInfo.Capture(ex);
            }
        }, maxStackSize: 32 * 1024 * 1024);
        thread.Start();
        thread.Join();
        captured?.Throw();
    }

    /// <summary>Emits <c>globals[name] = globals[name] + 1</c>, so a test can assert
    /// "this finally ran exactly N times" via the final value.</summary>
    private static void EmitIncrementGlobalCounter(Chunk chunk, int nameConstIdx, int line) {
        chunk.WriteOpCode(OpCode.GetGlobal, line); chunk.WriteByte((byte)nameConstIdx, line);
        int one = chunk.AddConstant(GrobValue.FromInt(1));
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte((byte)one, line);
        chunk.WriteOpCode(OpCode.AddInt, line);
        chunk.WriteOpCode(OpCode.SetGlobal, line); chunk.WriteByte((byte)nameConstIdx, line);
    }

    /// <summary>
    /// Builds <c>try { emitFaultingBody } catch (e: leafName) {}; reached := true</c>,
    /// runs it, and asserts the routed error was caught (the VM does not throw),
    /// bound at slot 0 with the expected leaf type name and message, and that the
    /// statement after the try executed — the D-039 "first unhandled runtime error"
    /// reading made observable: a caught one does not halt the VM.
    /// </summary>
    private static void AssertCaughtAndResumes(
            Action<Chunk> emitFaultingBody, string leafName, string expectedMessageSubstring) {
        var script = new Chunk();
        int reachedName = script.AddConstant(GrobValue.FromString("reached"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        emitFaultingBody(script);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count; // empty catch body — binds at slot 0

        PatchJump16(script, jumpSite);
        script.WriteOpCode(OpCode.TryEnd, 3);
        script.WriteOpCode(OpCode.True, 4);
        script.WriteOpCode(OpCode.DefineGlobal, 4); script.WriteByte((byte)reachedName, 4);
        script.WriteOpCode(OpCode.Return, 4);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler([leafName], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(0, vm.FrameCount);
        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal(leafName, s!.TypeName);
        Assert.Contains(expectedMessageSubstring, s.GetField("message").AsString());
        Assert.True(vm.Globals["reached"].AsBool());
    }

    // -----------------------------------------------------------------------
    // Each routed site is catchable, binds the right leaf and message, and the
    // script resumes after the try.
    // -----------------------------------------------------------------------

    [Fact]
    public void Catch_ArithmeticError_IntOverflow_CatchesAndResumes() {
        AssertCaughtAndResumes(script => {
            int max = script.AddConstant(GrobValue.FromInt(long.MaxValue));
            int one = script.AddConstant(GrobValue.FromInt(1));
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)max, 2);
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)one, 2);
            script.WriteOpCode(OpCode.AddInt, 2);
        }, "ArithmeticError", "integer overflow");
    }

    [Fact]
    public void Catch_ArithmeticError_IntDivideByZero_CatchesAndResumes() {
        AssertCaughtAndResumes(script => {
            int ten = script.AddConstant(GrobValue.FromInt(10));
            int zero = script.AddConstant(GrobValue.FromInt(0));
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)ten, 2);
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)zero, 2);
            script.WriteOpCode(OpCode.DivideInt, 2);
        }, "ArithmeticError", "integer division by zero");
    }

    [Fact]
    public void Catch_ArithmeticError_IntModuloByZero_CatchesAndResumes() {
        AssertCaughtAndResumes(script => {
            int ten = script.AddConstant(GrobValue.FromInt(10));
            int zero = script.AddConstant(GrobValue.FromInt(0));
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)ten, 2);
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)zero, 2);
            script.WriteOpCode(OpCode.ModuloInt, 2);
        }, "ArithmeticError", "integer modulo by zero");
    }

    [Fact]
    public void Catch_ArithmeticError_FloatDivideByZero_CatchesAndResumes() {
        AssertCaughtAndResumes(script => {
            int one = script.AddConstant(GrobValue.FromFloat(1.0));
            int zero = script.AddConstant(GrobValue.FromFloat(0.0));
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)one, 2);
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)zero, 2);
            script.WriteOpCode(OpCode.DivideFloat, 2);
        }, "ArithmeticError", "float division by zero");
    }

    [Fact]
    public void Catch_ArithmeticError_FloatModuloByZero_CatchesAndResumes() {
        AssertCaughtAndResumes(script => {
            int one = script.AddConstant(GrobValue.FromFloat(1.0));
            int zero = script.AddConstant(GrobValue.FromFloat(0.0));
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)one, 2);
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)zero, 2);
            script.WriteOpCode(OpCode.ModuloFloat, 2);
        }, "ArithmeticError", "float modulo by zero");
    }

    [Fact]
    public void Catch_IndexError_ArrayOutOfRange_CatchesAndResumes() {
        AssertCaughtAndResumes(script => {
            var arr = new GrobArray([GrobValue.FromInt(10)]);
            int arrConst = script.AddConstant(GrobValue.FromArray(arr));
            int idxConst = script.AddConstant(GrobValue.FromInt(5));
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)arrConst, 2);
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)idxConst, 2);
            script.WriteOpCode(OpCode.GetIndex, 2);
        }, "IndexError", "out of range");
    }

    /// <summary>
    /// Sprint 9 Increment A (D-348): the D-334 finally partition holds for a
    /// bounds throw exactly as it does for the ArithmeticError example proven
    /// generically below (<see cref="CaughtRoutedError_HandlerCompletesNormally_RunsFinallyOnce"/>) —
    /// a <c>finally</c> around an out-of-range <see cref="OpCode.GetIndex"/> runs
    /// exactly once, proving the array-index fault site correctly threads the
    /// finally-boundary parameters through <c>TryRaiseRuntimeGrobError</c>, not
    /// just that the mechanism works for some fault site.
    /// </summary>
    [Fact]
    public void Catch_IndexError_WithFinally_RunsFinallyExactlyOnce() {
        var script = new Chunk();
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int zero0 = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero0, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;

        var arr = new GrobArray([GrobValue.FromInt(10)]);
        int arrConst = script.AddConstant(GrobValue.FromArray(arr));
        int idxConst = script.AddConstant(GrobValue.FromInt(5));
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)arrConst, 3);
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)idxConst, 3);
        script.WriteOpCode(OpCode.GetIndex, 3);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 3);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 3); script.WriteByte(0xFF, 3);

        int handlerOffset = script.Count; // empty catch body — binds at slot 0

        PatchJump16(script, jumpSite);
        int finallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, counterName, 4);

        script.WriteOpCode(OpCode.TryEnd, 5);
        script.WriteOpCode(OpCode.Return, 5);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["IndexError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)], finallyOffset));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(1L, vm.Globals["ran"].AsInt());
    }

    [Fact]
    public void Catch_NilError_GetIndexOnNil_CatchesAndResumes() {
        AssertCaughtAndResumes(script => {
            int idxConst = script.AddConstant(GrobValue.FromInt(0));
            script.WriteOpCode(OpCode.Nil, 2);
            script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)idxConst, 2);
            script.WriteOpCode(OpCode.GetIndex, 2);
        }, "NilError", "nil dereference");
    }

    [Fact]
    public void Catch_NilError_GetPropertyOnNil_CatchesAndResumes() {
        AssertCaughtAndResumes(script => {
            int propConst = script.AddConstant(GrobValue.FromString("length"));
            script.WriteOpCode(OpCode.Nil, 2);
            script.WriteOpCode(OpCode.GetProperty, 2); script.WriteByte((byte)propConst, 2);
        }, "NilError", "nil dereference");
    }

    [Fact]
    public void Catch_RuntimeError_ValueStackOverflow_CatchesAndResumes() {
        AssertCaughtAndResumes(script => {
            int one = script.AddConstant(GrobValue.FromInt(1));
            for (int i = 0; i < ValueStack.Capacity + 1; i++) {
                script.WriteOpCode(OpCode.Constant, 2);
                script.WriteByte((byte)one, 2);
            }
        }, "RuntimeError", "value stack overflow");
    }

    // -----------------------------------------------------------------------
    // Call-stack-depth-exceeded (E5901) — two sites: the OpCode.Call arm and
    // the InvokeCallable re-entrant native bridge. Both are exercised via
    // self-recursion where EVERY level wraps its own recursive step in its own
    // try/catch — only the deepest level's call actually trips the guard, and
    // it is caught in the SAME frame it originates in (no cross-native-boundary
    // unwind required), so the recursion is self-limiting and the top-level
    // Run() completes normally instead of throwing.
    // -----------------------------------------------------------------------

    [Fact]
    public void Catch_RuntimeError_CallOpcodeStackOverflow_CatchesAndResumes() {
        // fn rec(): try { rec() } catch (e: RuntimeError) { caught := true }
        var fnChunk = new Chunk();
        int regionIndex = fnChunk.AddTryRegion();
        fnChunk.WriteOpCode(OpCode.TryBegin, 1); fnChunk.WriteByte((byte)regionIndex, 1);
        int startOffset = fnChunk.Count;

        int selfName = fnChunk.AddConstant(GrobValue.FromString("rec"));
        fnChunk.WriteOpCode(OpCode.GetGlobal, 1); fnChunk.WriteByte((byte)selfName, 1);
        fnChunk.WriteOpCode(OpCode.Call, 1); fnChunk.WriteByte(0, 1);
        fnChunk.WriteOpCode(OpCode.Pop, 1);
        int endOffset = fnChunk.Count;

        fnChunk.WriteOpCode(OpCode.Jump, 1);
        int jumpSite = fnChunk.Count;
        fnChunk.WriteByte(0xFF, 1); fnChunk.WriteByte(0xFF, 1);

        int handlerOffset = fnChunk.Count;
        int caughtName = fnChunk.AddConstant(GrobValue.FromString("caught"));
        fnChunk.WriteOpCode(OpCode.True, 1);
        fnChunk.WriteOpCode(OpCode.DefineGlobal, 1); fnChunk.WriteByte((byte)caughtName, 1);

        PatchJump16(fnChunk, jumpSite);
        fnChunk.WriteOpCode(OpCode.TryEnd, 1);
        fnChunk.WriteOpCode(OpCode.Nil, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);

        fnChunk.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["RuntimeError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var fn = new BytecodeFunction("rec", 0, fnChunk);

        var script = new Chunk();
        int fnConst = script.AddConstant(GrobValue.FromFunction(fn));
        int name = script.AddConstant(GrobValue.FromString("rec"));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)fnConst, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)name, 1);
        script.WriteOpCode(OpCode.GetGlobal, 1); script.WriteByte((byte)name, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Pop, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(0, vm.FrameCount);
        Assert.True(vm.Globals.TryGetValue("caught", out GrobValue caught) && caught.AsBool());
    }

    [Fact]
    public void Catch_RuntimeError_InvokeCallableStackOverflow_CatchesAndResumes() {
        // arr := [0]; self := leaf
        // fn leaf(): try { arr.each(self) } catch (e: RuntimeError) { caught := true }
        // each()'s native loop invokes its callback via the InvokeCallable
        // re-entrant bridge (not OpCode.Call), so recursing this way trips
        // InvokeCallable's own frame-depth check specifically.
        var leafChunk = new Chunk();
        int regionIndex = leafChunk.AddTryRegion();
        leafChunk.WriteOpCode(OpCode.TryBegin, 1); leafChunk.WriteByte((byte)regionIndex, 1);
        int startOffset = leafChunk.Count;

        int arrName = leafChunk.AddConstant(GrobValue.FromString("arr"));
        int selfName = leafChunk.AddConstant(GrobValue.FromString("self"));
        int eachProp = leafChunk.AddConstant(GrobValue.FromString("each"));
        leafChunk.WriteOpCode(OpCode.GetGlobal, 1); leafChunk.WriteByte((byte)arrName, 1);
        leafChunk.WriteOpCode(OpCode.GetProperty, 1); leafChunk.WriteByte((byte)eachProp, 1);
        leafChunk.WriteOpCode(OpCode.GetGlobal, 1); leafChunk.WriteByte((byte)selfName, 1);
        leafChunk.WriteOpCode(OpCode.Call, 1); leafChunk.WriteByte(1, 1);
        leafChunk.WriteOpCode(OpCode.Pop, 1);
        int endOffset = leafChunk.Count;

        leafChunk.WriteOpCode(OpCode.Jump, 1);
        int jumpSite = leafChunk.Count;
        leafChunk.WriteByte(0xFF, 1); leafChunk.WriteByte(0xFF, 1);

        int handlerOffset = leafChunk.Count;
        int caughtName = leafChunk.AddConstant(GrobValue.FromString("caught"));
        leafChunk.WriteOpCode(OpCode.True, 1);
        leafChunk.WriteOpCode(OpCode.DefineGlobal, 1); leafChunk.WriteByte((byte)caughtName, 1);

        PatchJump16(leafChunk, jumpSite);
        leafChunk.WriteOpCode(OpCode.TryEnd, 1);
        leafChunk.WriteOpCode(OpCode.Nil, 1);
        leafChunk.WriteOpCode(OpCode.Return, 1);

        leafChunk.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["RuntimeError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var leafFn = new BytecodeFunction("leaf", 0, leafChunk);

        var script = new Chunk();
        int arrConstName = script.AddConstant(GrobValue.FromString("arr"));
        int selfConstName = script.AddConstant(GrobValue.FromString("self"));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.NewArray, 1); script.WriteByte(1, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)arrConstName, 1);

        int leafFnIdx = script.AddConstant(GrobValue.FromFunction(leafFn));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)leafFnIdx, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)selfConstName, 1);

        script.WriteOpCode(OpCode.GetGlobal, 1); script.WriteByte((byte)selfConstName, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Pop, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        RunOnLargeStackThread(() => vm.Run(script));

        Assert.Equal(0, vm.FrameCount);
        Assert.True(vm.Globals.TryGetValue("caught", out GrobValue caught) && caught.AsBool());
    }

    // -----------------------------------------------------------------------
    // finally runs exactly once around a routed error, both caught and
    // propagating unhandled — and the unhandled path still surfaces the
    // ORIGINAL diagnostic (GrobArithmeticException/E5002), never the generic
    // E5904 unhandled-user-throw code, even though a finally ran on the way out.
    // -----------------------------------------------------------------------

    [Fact]
    public void CaughtRoutedError_HandlerCompletesNormally_RunsFinallyOnce() {
        var script = new Chunk();
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int zero0 = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero0, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;

        int ten = script.AddConstant(GrobValue.FromInt(10));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)ten, 3);
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)zero, 3);
        script.WriteOpCode(OpCode.DivideInt, 3);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 3);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 3); script.WriteByte(0xFF, 3);

        int handlerOffset = script.Count; // empty catch body — binds at slot 0

        PatchJump16(script, jumpSite);
        int finallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, counterName, 4);

        script.WriteOpCode(OpCode.TryEnd, 5);
        script.WriteOpCode(OpCode.Return, 5);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["ArithmeticError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)], finallyOffset));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(1L, vm.Globals["ran"].AsInt());
    }

    [Fact]
    public void UnhandledRoutedError_PropagatingPastTry_RunsFinallyThenOriginalDiagnosticSurfaces() {
        var script = new Chunk();
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int zero0 = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero0, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;

        int ten = script.AddConstant(GrobValue.FromInt(10));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)ten, 3);
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)zero, 3);
        script.WriteOpCode(OpCode.DivideInt, 3);
        int endOffset = script.Count;

        int finallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, counterName, 4);

        script.WriteOpCode(OpCode.TryEnd, 5);
        script.WriteOpCode(OpCode.Return, 5);

        // No handlers — nothing matches, so the routed error is unhandled.
        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset));

        var (vm, _) = NewVm();
        GrobArithmeticException ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5002.Code, ex.Code);
        Assert.Equal(3, ex.Line);
        Assert.Equal(0, ex.Column);
        Assert.Equal(1L, vm.Globals["ran"].AsInt());
    }

    // Regression: PR #115 review — a routed fault occurring INSIDE a finally
    // body (running on the exceptional-unwind path via the bounded RunDispatch
    // in RunFinallyExceptional) was not routed with the bounded-finally context
    // TryRaiseRuntimeGrobError needs. Unbounded, PropagateThrow could match an
    // outer region directly instead of raising FinallyEscapeException, letting
    // the bounded dispatch run arbitrary outer code (with real side effects)
    // before RunFinallyExceptional's own ip/chunk restoration silently discarded
    // that progress — and the ORIGINAL in-flight exception then surfaced as
    // if the finally had never thrown at all, violating D-275.
    [Fact]
    public void RoutedFaultInsideFinally_ReplacesInFlightException_OuterCatchCatchesIt() {
        // try {                            // outer: catch (e: ArithmeticError)
        //   try { nil[0] }                 // inner: no catch, has finally — routed NilError
        //   finally { 10 / 0 }             // routed ArithmeticError inside the bounded finally
        // } catch (e: ArithmeticError) { caught := true }
        // after := true
        var script = new Chunk();
        int caughtName = script.AddConstant(GrobValue.FromString("caught"));
        int afterName = script.AddConstant(GrobValue.FromString("after"));
        int idx0 = script.AddConstant(GrobValue.FromInt(0));
        int ten = script.AddConstant(GrobValue.FromInt(10));
        int zero = script.AddConstant(GrobValue.FromInt(0));

        int outerRegion = script.AddTryRegion();
        int innerRegion = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)outerRegion, 1);
        int outerStart = script.Count;

        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)innerRegion, 2);
        int innerStart = script.Count;
        script.WriteOpCode(OpCode.Nil, 2);
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)idx0, 2);
        script.WriteOpCode(OpCode.GetIndex, 2);
        int innerEnd = script.Count;

        int innerFinally = script.Count;
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)ten, 3);
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)zero, 3);
        script.WriteOpCode(OpCode.DivideInt, 3);
        script.WriteOpCode(OpCode.TryEnd, 3);

        int outerEnd = script.Count;
        script.WriteOpCode(OpCode.Jump, 4);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 4); script.WriteByte(0xFF, 4);

        int outerCatch = script.Count;
        script.WriteOpCode(OpCode.True, 4);
        script.WriteOpCode(OpCode.DefineGlobal, 4); script.WriteByte((byte)caughtName, 4);

        PatchJump16(script, jumpSite);
        script.WriteOpCode(OpCode.TryEnd, 5);
        script.WriteOpCode(OpCode.True, 6);
        script.WriteOpCode(OpCode.DefineGlobal, 6); script.WriteByte((byte)afterName, 6);
        script.WriteOpCode(OpCode.Return, 6);

        script.SetTryRegion(innerRegion, new TryRegion(innerStart, innerEnd, [], innerFinally));
        script.SetTryRegion(outerRegion, new TryRegion(outerStart, outerEnd,
            [new CatchHandler(["ArithmeticError"], IsCatchAll: false, outerCatch, BindingSlot: 0)]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.True(vm.Globals.TryGetValue("caught", out GrobValue caught) && caught.AsBool(),
            "the outer catch(ArithmeticError) should catch the exception the finally replaced the original NilError with");
        Assert.True(vm.Globals.TryGetValue("after", out GrobValue after) && after.AsBool(),
            "execution should resume after the outer try, not surface either exception unhandled");
        Assert.Equal(0, vm.FrameCount);
    }

    // -----------------------------------------------------------------------
    // A routed error is a GrobError subtype: a catch-all and a root
    // `catch (e: GrobError)` handler both catch it, exactly as they would a
    // user throw (Sprint 7B's existing polymorphic-matching handler table,
    // exercised here against a VM-detected fault instead of an OpCode.Throw).
    // -----------------------------------------------------------------------

    [Fact]
    public void CatchAll_CatchesRoutedError() {
        var script = new Chunk();
        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        var arr = new GrobArray([GrobValue.FromInt(10)]);
        int arrConst = script.AddConstant(GrobValue.FromArray(arr));
        int idxConst = script.AddConstant(GrobValue.FromInt(5));
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)arrConst, 2);
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)idxConst, 2);
        script.WriteOpCode(OpCode.GetIndex, 2);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count;

        PatchJump16(script, jumpSite);
        script.WriteOpCode(OpCode.TryEnd, 3);
        script.WriteOpCode(OpCode.Return, 3);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [
            new CatchHandler(["NetworkError"], IsCatchAll: false, handlerOffset, BindingSlot: 0),
            new CatchHandler([], IsCatchAll: true, handlerOffset, BindingSlot: 0),
        ]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(0, vm.FrameCount);
        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal("IndexError", s!.TypeName);
    }

    [Fact]
    public void RootGrobErrorCatch_CatchesRoutedError() {
        var script = new Chunk();
        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        int idxConst = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Nil, 2);
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)idxConst, 2);
        script.WriteOpCode(OpCode.GetIndex, 2);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count;

        PatchJump16(script, jumpSite);
        script.WriteOpCode(OpCode.TryEnd, 3);
        script.WriteOpCode(OpCode.Return, 3);

        // The full leaf-name expansion the compiler would emit for a
        // `catch (e: GrobError)` clause (D-274 polymorphic matching).
        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [
            new CatchHandler(["GrobError", "IoError", "NetworkError", "JsonError", "ProcessError",
                "NilError", "ArithmeticError", "IndexError", "ParseError", "LookupError", "RuntimeError"],
                IsCatchAll: false, handlerOffset, BindingSlot: 0),
        ]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(0, vm.FrameCount);
        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal("NilError", s!.TypeName);
    }

    // -----------------------------------------------------------------------
    // exit() vs a routed runtime error in the identical try/catch-all/finally
    // shape: exit() matches nothing (not even the catch-all or a root
    // GrobError handler) and runs no finally; the two must not be conflated.
    // The pre-existing ExitInsideTryFinally_RunsNoFinally_TerminatesWithExitCode
    // regression (finally-only, no catch handlers) already proves half of this;
    // this test closes the gap by adding a catch-all to the same shape.
    // -----------------------------------------------------------------------

    [Fact]
    public void ExitInsideTryWithCatchAllAndFinally_NotCaught_NoFinally_TerminatesWithExitCode() {
        var script = new Chunk();
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int caughtName = script.AddConstant(GrobValue.FromString("caught"));
        int zero = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;

        int seven = script.AddConstant(GrobValue.FromInt(7));
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)seven, 3);
        script.WriteOpCode(OpCode.Exit, 3);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 3);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 3); script.WriteByte(0xFF, 3);

        int handlerOffset = script.Count;
        script.WriteOpCode(OpCode.True, 4);
        script.WriteOpCode(OpCode.DefineGlobal, 4); script.WriteByte((byte)caughtName, 4);

        PatchJump16(script, jumpSite);
        int finallyOffset = script.Count;
        EmitIncrementGlobalCounter(script, counterName, 5);

        script.WriteOpCode(OpCode.TryEnd, 6);
        script.WriteOpCode(OpCode.Return, 6);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler([], IsCatchAll: true, handlerOffset, BindingSlot: 0)], finallyOffset));

        var (vm, _) = NewVm();
        GrobExitException ex = Assert.Throws<GrobExitException>(() => vm.Run(script));

        Assert.Equal(7, ex.Code);
        Assert.False(vm.Globals.ContainsKey("caught"));
        Assert.False(vm.Globals.TryGetValue("ran", out GrobValue ranValue) && ranValue.AsInt() == 1);
    }
}
