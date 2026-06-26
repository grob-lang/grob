using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 5 Increment D — <see cref="OpCode.Closure"/>,
/// <see cref="OpCode.GetUpvalue"/>, <see cref="OpCode.SetUpvalue"/> and
/// <see cref="OpCode.CloseUpvalue"/>. All chunks are hand-constructed;
/// no compiler dependency.
/// </summary>
/// <remarks>
/// The tests prove four properties required by the increment:
/// <list type="bullet">
///   <item>A closure increments its captured variable across calls (close-on-return).</item>
///   <item>Two closures from two separate enclosing-function calls hold independent state.</item>
///   <item>A closed upvalue outlives the enclosing frame and remains readable after return.</item>
///   <item>Two closures from the same enclosing call share one open upvalue, so a write
///     through one is visible via the other while the frame is live.</item>
/// </list>
/// </remarks>
public sealed class VirtualMachineClosureTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    // -----------------------------------------------------------------------
    // Shared helper: builds a makeCounter fn chunk.
    //
    // Bytecode shape (without dead code):
    //   Constant(0)               ; count := 0, at slot 0 of the frame
    //   Closure <lambdaFn> <1> <0> ; create closure capturing local slot 0
    //   Return                    ; return the closure
    //   Nil, Return               ; safety-net (unreachable)
    //
    // Lambda (incrementer) body:
    //   GetUpvalue(0)             ; push count
    //   Constant(1)               ; push 1
    //   AddInt                    ; count + 1
    //   SetUpvalue(0)             ; count = count + 1
    //   GetUpvalue(0)             ; push new count (return value)
    //   Return
    //   Nil, Return               ; safety-net (unreachable)
    // -----------------------------------------------------------------------
    private static BytecodeFunction BuildMakeCounterFn() {
        // Inner lambda (the counter body).
        var lambdaChunk = new Chunk();
        int one = lambdaChunk.AddConstant(GrobValue.FromInt(1));
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);   // push count
        lambdaChunk.WriteOpCode(OpCode.Constant, 1); lambdaChunk.WriteByte((byte)one, 1); // push 1
        lambdaChunk.WriteOpCode(OpCode.AddInt, 1);                                    // count + 1
        lambdaChunk.WriteOpCode(OpCode.SetUpvalue, 1); lambdaChunk.WriteByte(0, 1);   // count = result
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);   // push updated count
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        lambdaChunk.WriteOpCode(OpCode.Nil, 1);  // safety-net
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        // makeCounter body.
        var mcChunk = new Chunk();
        int zero = mcChunk.AddConstant(GrobValue.FromInt(0));
        int lambdaIdx = mcChunk.AddConstant(GrobValue.FromFunction(lambdaFn));

        mcChunk.WriteOpCode(OpCode.Constant, 1); mcChunk.WriteByte((byte)zero, 1);       // count := 0
        mcChunk.WriteOpCode(OpCode.Closure, 1); mcChunk.WriteByte((byte)lambdaIdx, 1);  // Closure ...
        mcChunk.WriteByte(1, 1);                                                           //   isLocal=1
        mcChunk.WriteByte(0, 1);                                                           //   slot=0 (count)
        mcChunk.WriteOpCode(OpCode.Return, 1);
        mcChunk.WriteOpCode(OpCode.Nil, 1);   // safety-net
        mcChunk.WriteOpCode(OpCode.Return, 1);

        return new BytecodeFunction("makeCounter", 0, mcChunk);
    }

    // -----------------------------------------------------------------------
    // Test 1: a closure increments its captured counter across calls.
    //
    // Script:
    //   c := makeCounter()          ; Closure at slot 0
    //   c()                         ; result=1; discard
    //   c()                         ; result=2; leave on stack
    //   Return
    //
    // Asserts: stack top = 2.
    // -----------------------------------------------------------------------

    [Fact]
    public void MakeCounter_IncrementsThroughClosedUpvalue() {
        BytecodeFunction makeCounterFn = BuildMakeCounterFn();

        var script = new Chunk();
        int mcIdx = script.AddConstant(GrobValue.FromFunction(makeCounterFn));

        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)mcIdx, 1); // push makeCounter
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);            // c = makeCounter()

        // First call: c()
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);   // push c
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);   // call c
        script.WriteOpCode(OpCode.Pop, 1);                                // discard result (1)

        // Second call: c()
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);   // push c
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);   // call c; result=2 on stack

        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(2L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Test 2: two closures from two separate makeCounter calls hold independent
    // counters — per-call capture independence.
    //
    // Script:
    //   c1 := makeCounter()
    //   c2 := makeCounter()
    //   c1(); c1()              ; c1 counts to 2
    //   c2()                    ; c2 counts to 1 independently
    //   Return
    //
    // Asserts: stack top = 1 (c2's independent count).
    // If closures shared state: c2() would return 3.
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoClosures_HaveIndependentCounters() {
        BytecodeFunction makeCounterFn = BuildMakeCounterFn();

        var script = new Chunk();
        int mcIdx = script.AddConstant(GrobValue.FromFunction(makeCounterFn));

        // c1 := makeCounter()  →  slot 0
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)mcIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        // c2 := makeCounter()  →  slot 1
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)mcIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        // c1() × 2
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Pop, 1);

        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Pop, 1);

        // c2()
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(1, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        // c2 has its own counter starting at 0; calling it once yields 1.
        Assert.Equal(1L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Test 3: a closed upvalue outlives the enclosing frame.
    //
    // After makeCounter returns, its frame (including the 'count' stack slot)
    // is gone. The returned closure must still read and write the heap-copied
    // upvalue correctly.
    //
    // Asserts: c() = 1 (first call after enclosing frame has been discarded).
    // -----------------------------------------------------------------------

    [Fact]
    public void ClosedUpvalue_OutlivesEnclosingFrame() {
        BytecodeFunction makeCounterFn = BuildMakeCounterFn();

        var script = new Chunk();
        int mcIdx = script.AddConstant(GrobValue.FromFunction(makeCounterFn));

        // c := makeCounter()  →  makeCounter's frame is gone after Call returns
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)mcIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        // c()  →  reads/writes through the closed (heap) upvalue
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(1L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Test 4: two closures created from the same enclosing call share one
    // open upvalue, so a write through the first is visible via the second
    // while the enclosing frame is live.
    //
    // sharedTest fn:
    //   x := 0                        ; local slot 0
    //   incClosure = () => x = x + 1  ; captures slot 0, call it inline
    //   <call incClosure>              ; x becomes 1 through the open upvalue
    //   getClosure = () => x           ; also captures slot 0 → SAME Upvalue object
    //   return getClosure
    //
    // Script:
    //   getter := sharedTest()
    //   getter()                       ; reads x = 1 (written by incClosure before return)
    //
    // Asserts: stack top = 1.
    // -----------------------------------------------------------------------

    [Fact]
    public void SharedOpenUpvalue_MutationVisibleThroughSharedUpvalueObject() {
        // incFn: increments x (upvalue 0) and returns nil.
        var incChunk = new Chunk();
        int one = incChunk.AddConstant(GrobValue.FromInt(1));
        incChunk.WriteOpCode(OpCode.GetUpvalue, 1); incChunk.WriteByte(0, 1);        // push x
        incChunk.WriteOpCode(OpCode.Constant, 1); incChunk.WriteByte((byte)one, 1); // push 1
        incChunk.WriteOpCode(OpCode.AddInt, 1);                                       // x + 1
        incChunk.WriteOpCode(OpCode.SetUpvalue, 1); incChunk.WriteByte(0, 1);        // x = result
        incChunk.WriteOpCode(OpCode.Nil, 1);
        incChunk.WriteOpCode(OpCode.Return, 1);
        var incFn = new BytecodeFunction(string.Empty, 0, incChunk, upvalueCount: 1);

        // getFn: returns x (upvalue 0).
        var getChunk = new Chunk();
        getChunk.WriteOpCode(OpCode.GetUpvalue, 1); getChunk.WriteByte(0, 1); // push x
        getChunk.WriteOpCode(OpCode.Return, 1);
        getChunk.WriteOpCode(OpCode.Nil, 1);   // safety-net
        getChunk.WriteOpCode(OpCode.Return, 1);
        var getFn = new BytecodeFunction(string.Empty, 0, getChunk, upvalueCount: 1);

        // sharedTest fn body:
        //   x := 0 (slot 0)
        //   Closure <incFn> isLocal=1 slot=0
        //   Call 0                   ; call incClosure inline (x → 1 via open upvalue)
        //   Pop                      ; discard nil result
        //   Closure <getFn> isLocal=1 slot=0  ; CaptureUpvalue(slot 0) REUSES the open upvalue
        //   Return                   ; close all upvalues at >= stackBase; return getClosure
        var stChunk = new Chunk();
        int zero = stChunk.AddConstant(GrobValue.FromInt(0));
        int incIdx = stChunk.AddConstant(GrobValue.FromFunction(incFn));
        int getIdx = stChunk.AddConstant(GrobValue.FromFunction(getFn));

        stChunk.WriteOpCode(OpCode.Constant, 1); stChunk.WriteByte((byte)zero, 1);   // x := 0
        stChunk.WriteOpCode(OpCode.Closure, 1); stChunk.WriteByte((byte)incIdx, 1); // incClosure
        stChunk.WriteByte(1, 1);                                                       //   isLocal=1
        stChunk.WriteByte(0, 1);                                                       //   slot=0
        stChunk.WriteOpCode(OpCode.Call, 1); stChunk.WriteByte(0, 1);             // call incClosure
        stChunk.WriteOpCode(OpCode.Pop, 1);                                            // discard nil
        stChunk.WriteOpCode(OpCode.Closure, 1); stChunk.WriteByte((byte)getIdx, 1);  // getClosure
        stChunk.WriteByte(1, 1);                                                        //   isLocal=1
        stChunk.WriteByte(0, 1);                                                        //   slot=0
        stChunk.WriteOpCode(OpCode.Return, 1);                                         // return getClosure
        stChunk.WriteOpCode(OpCode.Nil, 1);   // safety-net
        stChunk.WriteOpCode(OpCode.Return, 1);
        var sharedTestFn = new BytecodeFunction("sharedTest", 0, stChunk);

        // Script: getter := sharedTest(); getter()
        var script = new Chunk();
        int stIdx = script.AddConstant(GrobValue.FromFunction(sharedTestFn));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)stIdx, 1); // push sharedTest
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);            // getter at slot 0
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);            // push getter
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);            // call getter
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        // getClosure reads x=1 (the value written by incClosure through the shared upvalue).
        Assert.Equal(1L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // VM reuse after an abnormal exit (Addresses PR #88 review — CodeRabbit).
    //
    // A first run that opens an upvalue and then exits via exit() never reaches
    // Return, so _openUpvalues retains the open cell. Run() must clear that
    // per-run state so a second run on the same VM behaves like a fresh one and
    // CaptureUpvalue cannot deduplicate against the stale cell.
    // -----------------------------------------------------------------------

    [Fact]
    public void ReusedAfterAbnormalExit_OpenUpvalueDoesNotLeakIntoNextRun() {
        // Run 1: a function that opens an upvalue (Closure isLocal=1 slot 0) and then
        // exits before Return, leaving the open cell behind.
        var leakyFn = BuildOpenThenExitFn();
        var run1 = new Chunk();
        int leakyIdx = run1.AddConstant(GrobValue.FromFunction(leakyFn));
        run1.WriteOpCode(OpCode.Constant, 1); run1.WriteByte((byte)leakyIdx, 1);
        run1.WriteOpCode(OpCode.Call, 1); run1.WriteByte(0, 1);
        run1.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        Assert.Throws<GrobExitException>(() => vm.Run(run1));

        // Run 2 on the SAME vm: makeCounter() called once must yield 1, exactly as on
        // a fresh VM — the stale open cell from run 1 must not be reused.
        BytecodeFunction makeCounterFn = BuildMakeCounterFn();
        var run2 = new Chunk();
        int mcIdx = run2.AddConstant(GrobValue.FromFunction(makeCounterFn));
        run2.WriteOpCode(OpCode.Constant, 1); run2.WriteByte((byte)mcIdx, 1);
        run2.WriteOpCode(OpCode.Call, 1); run2.WriteByte(0, 1);     // c = makeCounter()
        run2.WriteOpCode(OpCode.GetLocal, 1); run2.WriteByte(0, 1); // push c
        run2.WriteOpCode(OpCode.Call, 1); run2.WriteByte(0, 1);     // c() -> 1
        run2.WriteOpCode(OpCode.Return, 1);

        vm.Run(run2);
        Assert.Equal(1L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // D-325 Escape matrix — Sprint 5 Increment 3.
    //
    // Tests M1–M6 verify that a closure whose open upvalue tracks a local of an
    // enclosing function has that upvalue correctly closed by
    // CloseUpvaluesFrom(_stackBase) on OP_RETURN, regardless of which container
    // the closure has been placed into by the time Return executes.
    //
    // Shared building block:
    //   enclosing fn: Constant(42) [slot 0]  Closure(lambda, isLocal=1, slot=0)  ...
    //   lambda body:  GetUpvalue(0)  Return
    //   Call result:  42 (the closed value)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns a <see cref="BytecodeFunction"/> that creates local <c>x = 42</c>
    /// and returns a closure capturing <c>x</c> directly (return value is the closure).
    /// </summary>
    private static BytecodeFunction BuildCapture42Fn() {
        var lambdaChunk = new Chunk();
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        lambdaChunk.WriteOpCode(OpCode.Nil, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        var outerChunk = new Chunk();
        int fortyTwo = outerChunk.AddConstant(GrobValue.FromInt(42));
        int lambdaIdx = outerChunk.AddConstant(GrobValue.FromFunction(lambdaFn));
        outerChunk.WriteOpCode(OpCode.Constant, 1); outerChunk.WriteByte((byte)fortyTwo, 1);
        outerChunk.WriteOpCode(OpCode.Closure, 1); outerChunk.WriteByte((byte)lambdaIdx, 1);
        outerChunk.WriteByte(1, 1);   // isLocal = 1
        outerChunk.WriteByte(0, 1);   // slot = 0 (x)
        outerChunk.WriteOpCode(OpCode.Return, 1);
        outerChunk.WriteOpCode(OpCode.Nil, 1);
        outerChunk.WriteOpCode(OpCode.Return, 1);
        return new BytecodeFunction("makeCapture42", 0, outerChunk);
    }

    /// <summary>
    /// Like <see cref="BuildCapture42Fn"/> but the closure is placed into a
    /// single-element array before Return — the return value is <c>[closure]</c>,
    /// not the closure itself. This is the key shape that fails under value-based
    /// closing and passes under location-based closing (D-325).
    /// </summary>
    private static BytecodeFunction BuildCapture42ViaArrayFn() {
        var lambdaChunk = new Chunk();
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        lambdaChunk.WriteOpCode(OpCode.Nil, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        var outerChunk = new Chunk();
        int fortyTwo = outerChunk.AddConstant(GrobValue.FromInt(42));
        int lambdaIdx = outerChunk.AddConstant(GrobValue.FromFunction(lambdaFn));
        outerChunk.WriteOpCode(OpCode.Constant, 1); outerChunk.WriteByte((byte)fortyTwo, 1); // x := 42 (slot 0)
        outerChunk.WriteOpCode(OpCode.Closure, 1); outerChunk.WriteByte((byte)lambdaIdx, 1);
        outerChunk.WriteByte(1, 1);                  // isLocal = 1
        outerChunk.WriteByte(0, 1);                  // slot = 0 (x)
        outerChunk.WriteOpCode(OpCode.NewArray, 1); outerChunk.WriteByte(1, 1);              // [closure] — closure now in container
        outerChunk.WriteOpCode(OpCode.Return, 1);                                             // return [closure], not closure
        outerChunk.WriteOpCode(OpCode.Nil, 1);
        outerChunk.WriteOpCode(OpCode.Return, 1);
        return new BytecodeFunction("makeCapture42ViaArray", 0, outerChunk);
    }

    // -----------------------------------------------------------------------
    // M1 — closure stored in an array at the time the enclosing frame exits.
    //
    // The return value is [closure], not the closure itself. Location-based
    // closing (D-325) closes slot 0 (x = 42) regardless; value-based closing
    // would miss it. After retrieval via GetIndex, the call must return 42.
    //
    // Script:
    //   arr := makeCapture42ViaArray()    ; [closure] at slot 0
    //   arr[0]()                          ; retrieve closure, call, result on stack
    // -----------------------------------------------------------------------

    [Fact]
    public void ClosureEscapedViaArray_ClosedUpvalue_CanBeCalledPostReturn() {
        BytecodeFunction makeCapture42ViaArrayFn = BuildCapture42ViaArrayFn();

        var script = new Chunk();
        int fnIdx = script.AddConstant(GrobValue.FromFunction(makeCapture42ViaArrayFn));
        int zero = script.AddConstant(GrobValue.FromInt(0));

        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)fnIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);              // arr at slot 0

        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);              // push arr
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero, 1);     // push 0
        script.WriteOpCode(OpCode.GetIndex, 1);                                       // arr[0] → closure

        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);               // closure() → 42
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // M2 — closure retrieved from a map by string key.
    //
    // The closed closure is obtained via BuildCapture42Fn (direct return; upvalue
    // already closed by CloseUpvaluesFrom in phase 1) and stored in a GrobMap
    // in C#. A second Run retrieves it by key via GetIndex and calls it.
    // Proves: a closure held in a map is callable with its closed upvalue intact.
    // -----------------------------------------------------------------------

    [Fact]
    public void ClosureFromMap_RetrievedByKey_ReturnsCapture() {
        BytecodeFunction makeCapture42Fn = BuildCapture42Fn();

        // Phase 1: obtain a closed closure on the stack.
        var phase1 = new Chunk();
        int fnIdx1 = phase1.AddConstant(GrobValue.FromFunction(makeCapture42Fn));
        phase1.WriteOpCode(OpCode.Constant, 1); phase1.WriteByte((byte)fnIdx1, 1);
        phase1.WriteOpCode(OpCode.Call, 1); phase1.WriteByte(0, 1);
        phase1.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(phase1);
        GrobValue closureVal = vm.Stack.Peek();   // closed closure, upvalue = 42

        // Store in a map and retrieve by key.
        var map = new GrobMap();
        map.Set("fn", closureVal);

        var phase2 = new Chunk();
        int mapIdx = phase2.AddConstant(GrobValue.FromMap(map));
        int keyIdx = phase2.AddConstant(GrobValue.FromString("fn"));

        phase2.WriteOpCode(OpCode.Constant, 1); phase2.WriteByte((byte)mapIdx, 1);  // push map
        phase2.WriteOpCode(OpCode.Constant, 1); phase2.WriteByte((byte)keyIdx, 1);  // push "fn"
        phase2.WriteOpCode(OpCode.GetIndex, 1);                                       // map["fn"] → closure
        phase2.WriteOpCode(OpCode.Call, 1); phase2.WriteByte(0, 1);             // closure() → 42
        phase2.WriteOpCode(OpCode.Return, 1);

        vm.Run(phase2);

        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // M3 — closure returned from enclosing fn and immediately invoked.
    //
    // Equivalent to makeCapture42()(). No intermediate storage; the result of
    // the first Call is the callee of the second Call. Proves the upvalue is
    // correctly closed by OP_RETURN before the second Call reads it.
    // -----------------------------------------------------------------------

    [Fact]
    public void ClosureReturnedThenImmediatelyInvoked_ReturnsCapture() {
        BytecodeFunction makeCapture42Fn = BuildCapture42Fn();

        var script = new Chunk();
        int fnIdx = script.AddConstant(GrobValue.FromFunction(makeCapture42Fn));

        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)fnIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);  // → closure (slot 0)
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);  // closure() → 42
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // M4 — closure passed as a parameter to a different function, called there.
    //
    // callerFn(closure): GetLocal(0) [param], Call(0), Return.
    // Proves a closed upvalue is readable when the closure executes inside
    // a call frame that is not related to the enclosing capture frame.
    // -----------------------------------------------------------------------

    [Fact]
    public void ClosurePassedAsParameter_CalledByCallee_ReturnsCapture() {
        BytecodeFunction makeCapture42Fn = BuildCapture42Fn();

        // callerFn(closure): pushes param, calls it, returns result.
        var callerChunk = new Chunk();
        callerChunk.WriteOpCode(OpCode.GetLocal, 1); callerChunk.WriteByte(0, 1);  // push param (the closure)
        callerChunk.WriteOpCode(OpCode.Call, 1); callerChunk.WriteByte(0, 1);  // closure() → 42
        callerChunk.WriteOpCode(OpCode.Return, 1);
        callerChunk.WriteOpCode(OpCode.Nil, 1);
        callerChunk.WriteOpCode(OpCode.Return, 1);
        var callerFn = new BytecodeFunction("caller", 1, callerChunk);

        var script = new Chunk();
        int makeCapIdx = script.AddConstant(GrobValue.FromFunction(makeCapture42Fn));
        int callerIdx = script.AddConstant(GrobValue.FromFunction(callerFn));

        // closure := makeCapture42()  →  slot 0
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)makeCapIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        // caller(closure)
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)callerIdx, 1);  // callerFn
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);                 // closure as arg
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(1, 1);                 // caller(closure)

        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // M5 — counter closure escaped via array; SetUpvalue mutations persist.
    //
    // makeCounterViaArray returns [counter]. counter() increments its closed
    // upvalue (SetUpvalue writes to the heap copy). Calling the counter twice
    // through array[0] must yield 1 then 2 — mutation state accumulates even
    // after the enclosing frame has been discarded.
    //
    // Asserts: second call = 2.
    // -----------------------------------------------------------------------

    [Fact]
    public void CounterClosureEscapedViaArray_MutationPersistsAcrossMultipleCalls() {
        // Incrementer lambda: count+1 → count; return new count.
        var lambdaChunk = new Chunk();
        int one = lambdaChunk.AddConstant(GrobValue.FromInt(1));
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Constant, 1); lambdaChunk.WriteByte((byte)one, 1);
        lambdaChunk.WriteOpCode(OpCode.AddInt, 1);
        lambdaChunk.WriteOpCode(OpCode.SetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        lambdaChunk.WriteOpCode(OpCode.Nil, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        // makeCounterViaArray: count := 0 (slot 0), closure in [closure], return array.
        var outerChunk = new Chunk();
        int zero = outerChunk.AddConstant(GrobValue.FromInt(0));
        int lambdaIdx = outerChunk.AddConstant(GrobValue.FromFunction(lambdaFn));
        outerChunk.WriteOpCode(OpCode.Constant, 1); outerChunk.WriteByte((byte)zero, 1);
        outerChunk.WriteOpCode(OpCode.Closure, 1); outerChunk.WriteByte((byte)lambdaIdx, 1);
        outerChunk.WriteByte(1, 1); outerChunk.WriteByte(0, 1);         // isLocal=1, slot=0
        outerChunk.WriteOpCode(OpCode.NewArray, 1); outerChunk.WriteByte(1, 1);
        outerChunk.WriteOpCode(OpCode.Return, 1);
        outerChunk.WriteOpCode(OpCode.Nil, 1);
        outerChunk.WriteOpCode(OpCode.Return, 1);
        var makeCounterViaArrayFn = new BytecodeFunction("makeCounterViaArray", 0, outerChunk);

        var script = new Chunk();
        int fnIdx = script.AddConstant(GrobValue.FromFunction(makeCounterViaArrayFn));
        int idx0 = script.AddConstant(GrobValue.FromInt(0));

        // arr := makeCounterViaArray()  →  slot 0
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)fnIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        // First call: arr[0]() → 1; discard.
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)idx0, 1);
        script.WriteOpCode(OpCode.GetIndex, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Pop, 1);

        // Second call: arr[0]() → 2; leave on stack.
        script.WriteOpCode(OpCode.GetLocal, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)idx0, 1);
        script.WriteOpCode(OpCode.GetIndex, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);

        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(2L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // M6 — closure stored as a struct field, retrieved via GetProperty, called.
    //
    // RED until GetProperty handles GrobStruct receivers (D-325 scope).
    // The closed closure is placed into a GrobStruct field "fn" in C#.
    // A second Run retrieves it via GetProperty and calls it.
    // -----------------------------------------------------------------------

    [Fact]
    public void ClosureFromStruct_FieldAccessAndCall_ReturnsCapture() {
        BytecodeFunction makeCapture42Fn = BuildCapture42Fn();

        // Phase 1: obtain a closed closure on the stack.
        var phase1 = new Chunk();
        int fnIdx1 = phase1.AddConstant(GrobValue.FromFunction(makeCapture42Fn));
        phase1.WriteOpCode(OpCode.Constant, 1); phase1.WriteByte((byte)fnIdx1, 1);
        phase1.WriteOpCode(OpCode.Call, 1); phase1.WriteByte(0, 1);
        phase1.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(phase1);
        GrobValue closureVal = vm.Stack.Peek();  // closed closure, upvalue = 42

        // Build a GrobStruct with the closure at field "fn".
        var s = new GrobStruct("Container");
        s.SetField("fn", closureVal);

        // Phase 2: struct.fn()  →  42.
        var phase2 = new Chunk();
        int structIdx = phase2.AddConstant(GrobValue.FromStruct(s));
        int propIdx = phase2.AddConstant(GrobValue.FromString("fn"));

        phase2.WriteOpCode(OpCode.Constant, 1); phase2.WriteByte((byte)structIdx, 1); // push struct
        phase2.WriteOpCode(OpCode.GetProperty, 1); phase2.WriteByte((byte)propIdx, 1);   // struct.fn → closure
        phase2.WriteOpCode(OpCode.Call, 1); phase2.WriteByte(0, 1);               // closure() → 42
        phase2.WriteOpCode(OpCode.Return, 1);

        vm.Run(phase2);

        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    // Function body: Constant(7) [local slot 0], Closure(isLocal=1 slot 0) opens an
    // upvalue, then Constant(0) + Exit terminate before Return runs.
    private static BytecodeFunction BuildOpenThenExitFn() {
        var inner = new Chunk();
        inner.WriteOpCode(OpCode.GetUpvalue, 1); inner.WriteByte(0, 1);
        inner.WriteOpCode(OpCode.Return, 1);
        inner.WriteOpCode(OpCode.Nil, 1);
        inner.WriteOpCode(OpCode.Return, 1);
        var innerFn = new BytecodeFunction(string.Empty, 0, inner, upvalueCount: 1);

        var chunk = new Chunk();
        int seven = chunk.AddConstant(GrobValue.FromInt(7));
        int lambdaIdx = chunk.AddConstant(GrobValue.FromFunction(innerFn));
        int zero = chunk.AddConstant(GrobValue.FromInt(0));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)seven, 1);     // local slot 0
        chunk.WriteOpCode(OpCode.Closure, 1); chunk.WriteByte((byte)lambdaIdx, 1);  // open upvalue
        chunk.WriteByte(1, 1); chunk.WriteByte(0, 1);                                //   isLocal=1 slot=0
        chunk.WriteOpCode(OpCode.Pop, 1);                                            // discard closure
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)zero, 1);       // exit code 0
        chunk.WriteOpCode(OpCode.Exit, 1);                                           // abnormal exit
        return new BytecodeFunction("leaky", 0, chunk);
    }
}
