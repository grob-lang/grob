using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 5 Increment E — the top-level
/// initialisation state machine (§19.1, D-294). Each top-level binding slot
/// carries a <c>SlotState</c> tag; <see cref="OpCode.GetGlobal"/> consults it
/// during startup and raises <c>E5902</c> (circular initialisation) on a read
/// of a slot that is not yet <c>Initialised</c>. All chunks are
/// hand-constructed; no compiler dependency.
/// </summary>
/// <remarks>
/// The tests prove the properties required by the increment:
/// <list type="bullet">
///   <item>A <c>GetGlobal</c> after the slot's <c>DefineGlobal</c> completes succeeds.</item>
///   <item>A <c>GetGlobal</c> on a slot the chunk defines later (so it is pre-scanned
///     but not yet initialised) raises <c>E5902</c> during startup.</item>
///   <item>A circular initialisation — a function read of a top-level binding that has
///     not yet been initialised — raises <c>E5902</c>.</item>
///   <item>A steady-state read of an already-initialised global from a function body
///     succeeds (no spurious <c>E5902</c>).</item>
/// </list>
/// </remarks>
public sealed class VirtualMachineGlobalInitTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte NameByte(Chunk chunk, string name) =>
        (byte)chunk.AddConstant(GrobValue.FromString(name));

    private static byte IntByte(Chunk chunk, long value) =>
        (byte)chunk.AddConstant(GrobValue.FromInt(value));

    // -----------------------------------------------------------------------
    // Baseline: a read after the slot's DefineGlobal completes succeeds.
    // -----------------------------------------------------------------------

    [Fact]
    public void DefineThenGet_AfterInitialisation_Succeeds() {
        // x := 42; print(x)
        var chunk = new Chunk();
        byte xName = NameByte(chunk, "x");
        byte i42 = IntByte(chunk, 42L);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i42, 1);
        chunk.WriteOpCode(OpCode.DefineGlobal, 1); chunk.WriteByte(xName, 1);
        chunk.WriteOpCode(OpCode.GetGlobal, 2); chunk.WriteByte(xName, 2);
        chunk.WriteOpCode(OpCode.Print, 2);
        chunk.WriteOpCode(OpCode.Return, 2);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Equal($"42{Environment.NewLine}", output.ToString());
        Assert.Equal(0, vm.Stack.Count);
    }

    // -----------------------------------------------------------------------
    // A read of a slot the chunk defines LATER (pre-scanned Uninitialised)
    // raises E5902 during startup — distinct from E1001 (never defined).
    // -----------------------------------------------------------------------

    [Fact]
    public void GetGlobal_BeforeItsDefineGlobal_DuringStartup_ThrowsE5902() {
        // Bytecode reads x before the DefineGlobal that the pre-scan sees:
        //   GetGlobal x   ; x is pre-scanned as Uninitialised -> E5902
        //   Constant 42
        //   DefineGlobal x
        //   Return
        var chunk = new Chunk();
        byte xName = NameByte(chunk, "x");
        byte i42 = IntByte(chunk, 42L);
        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte(xName, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(i42, 1);
        chunk.WriteOpCode(OpCode.DefineGlobal, 1); chunk.WriteByte(xName, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));
        Assert.Equal("E5902", ex.Code);
    }

    // -----------------------------------------------------------------------
    // Circular initialisation: a top-level initialiser calls a function that
    // reads the binding currently being initialised. The read sees a slot that
    // is not yet Initialised -> E5902.
    // -----------------------------------------------------------------------

    [Fact]
    public void CircularInitialisation_ViaFunctionCall_ThrowsE5902() {
        // fn foo(): reads A and returns it.
        var fooChunk = new Chunk();
        byte aNameInFoo = NameByte(fooChunk, "A");
        fooChunk.WriteOpCode(OpCode.GetGlobal, 1); fooChunk.WriteByte(aNameInFoo, 1);
        fooChunk.WriteOpCode(OpCode.Return, 1);
        var foo = new BytecodeFunction("foo", 0, fooChunk);

        // script: readonly A := foo()
        //   Constant <foo>
        //   Call 0          ; foo reads A while A is still Uninitialised -> E5902
        //   DefineGlobal A  ; never reached
        //   Return
        var script = new Chunk();
        byte aName = NameByte(script, "A");
        int fooConst = script.AddConstant(GrobValue.FromFunction(foo));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)fooConst, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte(aName, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));
        Assert.Equal("E5902", ex.Code);
    }

    // -----------------------------------------------------------------------
    // The E5902 message traces through the function (D-321, §19.1): it names the
    // binding being initialised, the function that performed the read, and the
    // uninitialised binding read, each with its source line.
    // -----------------------------------------------------------------------

    [Fact]
    public void CircularInitialisation_Message_TracesThroughTheFunction() {
        // fn computeA(): reads B and returns it. (B is declared later.)
        var computeAChunk = new Chunk();
        byte bNameInA = NameByte(computeAChunk, "B");
        computeAChunk.WriteOpCode(OpCode.GetGlobal, 5); computeAChunk.WriteByte(bNameInA, 5);
        computeAChunk.WriteOpCode(OpCode.Return, 5);
        var computeA = new BytecodeFunction("computeA", 0, computeAChunk);

        // script (functions hoisted into the prologue):
        //   Constant <computeA>; DefineGlobal computeA   ; line 4 — Initialised in prologue
        //   Constant <computeA>; Call 0                  ; line 1 — A's initialiser reads B -> E5902
        //   DefineGlobal A                               ; line 1 — A is the binding being initialised
        //   Constant 0; DefineGlobal B                   ; line 2 — B declared later
        //   Return
        var script = new Chunk();
        byte aName = NameByte(script, "A");
        byte bName = NameByte(script, "B");
        byte zero = IntByte(script, 0L);
        int computeAConst = script.AddConstant(GrobValue.FromFunction(computeA));
        script.WriteOpCode(OpCode.Constant, 4); script.WriteByte((byte)computeAConst, 4);
        script.WriteOpCode(OpCode.DefineGlobal, 4); script.WriteByte(NameByte(script, "computeA"), 4);
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)computeAConst, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte(aName, 1);
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte(zero, 2);
        script.WriteOpCode(OpCode.DefineGlobal, 2); script.WriteByte(bName, 2);
        script.WriteOpCode(OpCode.Return, 2);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal("E5902", ex.Code);
        // Binding being initialised: A on line 1.
        Assert.Contains("'A'", ex.Message);
        Assert.Contains("line 1", ex.Message);
        // The function that performed the read, with its declaration line (line 4).
        Assert.Contains("computeA", ex.Message);
        Assert.Contains("line 4", ex.Message);
        // The uninitialised binding read: B on line 2.
        Assert.Contains("'B'", ex.Message);
        Assert.Contains("line 2", ex.Message);
    }

    // -----------------------------------------------------------------------
    // The E5902 trace survives the re-entrant native bridge: when a native
    // invokes a Grob function (via the VmInvoker) during startup and that
    // function reads an uninitialised binding, InvokeCallable's frame carries
    // the callee so the message still names the function (D-321).
    // -----------------------------------------------------------------------

    [Fact]
    public void CircularInitialisation_ThroughNativeBridge_NamesTheFunction() {
        // fn reader(): reads B (declared later) and returns it.
        var readerChunk = new Chunk();
        byte bInReader = NameByte(readerChunk, "B");
        readerChunk.WriteOpCode(OpCode.GetGlobal, 7); readerChunk.WriteByte(bInReader, 7);
        readerChunk.WriteOpCode(OpCode.Return, 7);
        var reader = new BytecodeFunction("reader", 0, readerChunk);

        // Native 'apply' invokes its single function argument with no args.
        var apply = new NativeFunction("apply", 1, (args, invoke) => invoke(args[0], []));

        // script:
        //   Constant <reader>; DefineGlobal reader       ; prologue — Initialised
        //   GetGlobal apply; Constant <reader>; Call 1    ; A's initialiser: apply(reader)
        //                                                 ;   -> reader reads B (Uninit) -> E5902
        //   DefineGlobal A                                ; A being initialised
        //   Constant 0; DefineGlobal B                    ; B declared later
        //   Return
        var script = new Chunk();
        byte aName = NameByte(script, "A");
        byte bName = NameByte(script, "B");
        byte applyName = NameByte(script, "apply");
        byte zero = IntByte(script, 0L);
        int readerConst = script.AddConstant(GrobValue.FromFunction(reader));
        script.WriteOpCode(OpCode.Constant, 7); script.WriteByte((byte)readerConst, 7);
        script.WriteOpCode(OpCode.DefineGlobal, 7); script.WriteByte(NameByte(script, "reader"), 7);
        script.WriteOpCode(OpCode.GetGlobal, 1); script.WriteByte(applyName, 1);
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)readerConst, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(1, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte(aName, 1);
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte(zero, 2);
        script.WriteOpCode(OpCode.DefineGlobal, 2); script.WriteByte(bName, 2);
        script.WriteOpCode(OpCode.Return, 2);

        var (vm, _) = NewVm();
        vm.RegisterNative("apply", apply);
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal("E5902", ex.Code);
        // The trace names the function invoked through the native bridge.
        Assert.Contains("reader", ex.Message);
        Assert.Contains("'B'", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Steady-state read: a function called during startup reads a global that
    // has already been initialised. The slot is Initialised, so the startup
    // check passes and no E5902 fires.
    // -----------------------------------------------------------------------

    [Fact]
    public void SteadyStateRead_OfInitialisedGlobal_FromFunctionBody_Succeeds() {
        // fn bump(): return A + 1
        var bumpChunk = new Chunk();
        byte aNameInBump = NameByte(bumpChunk, "A");
        byte oneInBump = IntByte(bumpChunk, 1L);
        bumpChunk.WriteOpCode(OpCode.GetGlobal, 1); bumpChunk.WriteByte(aNameInBump, 1);
        bumpChunk.WriteOpCode(OpCode.Constant, 1); bumpChunk.WriteByte(oneInBump, 1);
        bumpChunk.WriteOpCode(OpCode.AddInt, 1);
        bumpChunk.WriteOpCode(OpCode.Return, 1);
        var bump = new BytecodeFunction("bump", 0, bumpChunk);

        // script: A := 10; print(bump())  → 11
        //   Constant 10
        //   DefineGlobal A      ; A becomes Initialised
        //   Constant <bump>
        //   Call 0              ; bump reads A (Initialised) -> 11
        //   Print
        //   Return
        var script = new Chunk();
        byte aName = NameByte(script, "A");
        byte ten = IntByte(script, 10L);
        int bumpConst = script.AddConstant(GrobValue.FromFunction(bump));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte(ten, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte(aName, 1);
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)bumpConst, 2);
        script.WriteOpCode(OpCode.Call, 2); script.WriteByte(0, 2);
        script.WriteOpCode(OpCode.Print, 2);
        script.WriteOpCode(OpCode.Return, 2);

        var (vm, output) = NewVm();
        vm.Run(script);

        Assert.Equal($"11{Environment.NewLine}", output.ToString());
    }

    // -----------------------------------------------------------------------
    // The pre-scan walks variable-length Closure instructions correctly: a
    // top-level closure bound to a global is skipped by the right number of
    // bytes so the following DefineGlobal is still found and tagged.
    // -----------------------------------------------------------------------

    [Fact]
    public void PreScan_TopLevelClosureBoundToGlobal_DefineGlobalStillTagged() {
        // Inner lambda body (never called): GetUpvalue 0; Return.
        var lambdaChunk = new Chunk();
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        // script:
        //   Constant 0        ; local at slot 0 (the captured variable)
        //   Closure <fn> 1 0  ; closure capturing local slot 0 (variable-length: 4 bytes)
        //   DefineGlobal f    ; store the closure as global f
        //   Pop               ; discard the local
        //   Return
        var script = new Chunk();
        byte fName = NameByte(script, "f");
        byte zero = IntByte(script, 0L);
        int lambdaConst = script.AddConstant(GrobValue.FromFunction(lambdaFn));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte(zero, 1);
        script.WriteOpCode(OpCode.Closure, 1);
        script.WriteByte((byte)lambdaConst, 1);
        script.WriteByte(1, 1); // isLocal
        script.WriteByte(0, 1); // capture slot 0
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte(fName, 1);
        script.WriteOpCode(OpCode.Pop, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.True(vm.Globals.TryGetValue("f", out GrobValue f));
        Assert.True(f.TryAsFunction(out _));
    }
}
