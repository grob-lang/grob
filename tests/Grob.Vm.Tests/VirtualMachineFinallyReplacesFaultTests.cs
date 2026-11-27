using Grob.Core;

using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM tests for a <c>finally</c> that throws while an error the VM or a native raised is in
/// flight (R-99, D-462). The finally's error replaces the fault (D-275): an outer <c>catch</c>
/// receives the replacement, and when nothing catches it the run ends with E5904 naming the
/// replacement, located where the fault was raised, as an uncaught <c>throw</c> replaced the same
/// way is (D-458). A finally that does not throw leaves the fault to surface with its own code.
/// Each route by which a fault reaches the handler walk is a row, but for
/// <c>InvokeCallable</c>'s own frame-depth check, which no row reaches: a callback's frame is
/// pushed below the limit, and the <c>Call</c> arm's check trips first inside it. All chunks
/// are hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineFinallyReplacesFaultTests {
    /// <summary>Line of every route's faulting instruction.</summary>
    private const int FaultLine = 3;

    /// <summary>Column of every route's faulting instruction.</summary>
    private const int FaultColumn = 9;

    /// <summary>Line of the finally's replacing <c>throw</c>, never the line a diagnostic names.</summary>
    private const int ReplaceLine = 5;

    private const string ReplacedDiagnostic = "RuntimeError: replaced";

    private static byte ConstByte(Chunk chunk, GrobValue value) => (byte)chunk.AddConstant(value);

    private static void PatchJump16(Chunk chunk, int patchSite) {
        int offset = chunk.Count - (patchSite + 2);
        chunk.PatchByte(patchSite, (byte)(offset >> 8));
        chunk.PatchByte(patchSite + 1, (byte)(offset & 0xFF));
    }

    private static void EmitConstant(Chunk chunk, GrobValue value, int line, int column = 0) {
        chunk.WriteOpCode(OpCode.Constant, line, column);
        chunk.WriteByte(ConstByte(chunk, value), line, column);
    }

    private static void EmitPrintString(Chunk chunk, string text, int line) {
        EmitConstant(chunk, GrobValue.FromString(text), line);
        chunk.WriteOpCode(OpCode.Print, line);
    }

    /// <summary>Emits <c>throw RuntimeError { message: <paramref name="message"/> }</c> at <paramref name="line"/>.</summary>
    private static void EmitThrowRuntimeError(Chunk chunk, string message, int line) {
        byte type = chunk.AddStructType(new StructTypeDescriptor("RuntimeError", ["message"]));
        EmitConstant(chunk, GrobValue.FromString(message), line);
        chunk.WriteOpCode(OpCode.NewStruct, line); chunk.WriteByte(type, line);
        chunk.WriteOpCode(OpCode.Throw, line);
    }

    /// <summary>Emits <paramref name="op"/> at the fault position, with an optional one-byte operand.</summary>
    private static void EmitAtFault(Chunk chunk, OpCode op, byte? operand = null) {
        chunk.WriteOpCode(op, FaultLine, FaultColumn);
        if (operand is byte b) chunk.WriteByte(b, FaultLine, FaultColumn);
    }

    private static NativeFunction Faulting(string leaf, string code) =>
        new("faulty", 0, (_, _) => throw new NativeFaultException(leaf, code, "bad input"));

    /// <summary><c>x =&gt; x / 0</c>, the division at the fault position.</summary>
    private static BytecodeFunction BuildDivideByZeroCallback() {
        var fnChunk = new Chunk();
        fnChunk.WriteOpCode(OpCode.GetLocal, FaultLine); fnChunk.WriteByte(0, FaultLine);
        EmitConstant(fnChunk, GrobValue.FromInt(0), FaultLine);
        EmitAtFault(fnChunk, OpCode.DivideInt);
        fnChunk.WriteOpCode(OpCode.Return, FaultLine);
        return new BytecodeFunction("", 1, fnChunk);
    }

    /// <summary>Emits <c>[1, 2].each(callback)</c> at line 2, leaving its result on the stack.</summary>
    private static void EmitEach(Chunk chunk, BytecodeFunction callback) {
        GrobValue array = GrobValue.FromArray(new GrobArray([GrobValue.FromInt(1), GrobValue.FromInt(2)]));
        EmitConstant(chunk, array, 2);
        chunk.WriteOpCode(OpCode.GetProperty, 2); chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("each")), 2);
        EmitConstant(chunk, GrobValue.FromFunction(callback), 2);
        chunk.WriteOpCode(OpCode.Call, 2); chunk.WriteByte(1, 2);
    }

    /// <summary>
    /// <c>rec := fn() { rec() }</c> defined and called: the call that finds every frame in use
    /// is at the fault position.
    /// </summary>
    private static void EmitCallDepthFault(Chunk chunk) {
        var fnChunk = new Chunk();
        byte self = ConstByte(fnChunk, GrobValue.FromString("rec"));
        fnChunk.WriteOpCode(OpCode.GetGlobal, FaultLine); fnChunk.WriteByte(self, FaultLine);
        EmitAtFault(fnChunk, OpCode.Call, 0);
        fnChunk.WriteOpCode(OpCode.Return, FaultLine);
        var rec = new BytecodeFunction("rec", 0, fnChunk);

        byte name = ConstByte(chunk, GrobValue.FromString("rec"));
        EmitConstant(chunk, GrobValue.FromFunction(rec), 2);
        chunk.WriteOpCode(OpCode.DefineGlobal, 2); chunk.WriteByte(name, 2);
        chunk.WriteOpCode(OpCode.GetGlobal, 2); chunk.WriteByte(name, 2);
        chunk.WriteOpCode(OpCode.Call, 2); chunk.WriteByte(0, 2);
    }

    /// <summary>
    /// <c>leaf := fn() { arr.each(leaf) }</c> with <c>arr := [0]</c>, defined and called: each
    /// level runs the next as a native's callback, so every frame past the first is a callback's.
    /// The <c>Call</c> arm's frame-depth check trips at the <c>each</c> call at the fault position,
    /// in the deepest callback, and the error leaves every callback's dispatch in turn (D-458).
    /// </summary>
    private static void EmitCallbackDepthFault(Chunk chunk) {
        var leafChunk = new Chunk();
        byte arr = ConstByte(leafChunk, GrobValue.FromString("arr"));
        byte self = ConstByte(leafChunk, GrobValue.FromString("leaf"));
        leafChunk.WriteOpCode(OpCode.GetGlobal, FaultLine); leafChunk.WriteByte(arr, FaultLine);
        leafChunk.WriteOpCode(OpCode.GetProperty, FaultLine);
        leafChunk.WriteByte(ConstByte(leafChunk, GrobValue.FromString("each")), FaultLine);
        leafChunk.WriteOpCode(OpCode.GetGlobal, FaultLine); leafChunk.WriteByte(self, FaultLine);
        EmitAtFault(leafChunk, OpCode.Call, 1);
        leafChunk.WriteOpCode(OpCode.Return, FaultLine);
        var leaf = new BytecodeFunction("leaf", 0, leafChunk);

        byte arrName = ConstByte(chunk, GrobValue.FromString("arr"));
        byte leafName = ConstByte(chunk, GrobValue.FromString("leaf"));
        EmitConstant(chunk, GrobValue.FromArray(new GrobArray([GrobValue.FromInt(0)])), 2);
        chunk.WriteOpCode(OpCode.DefineGlobal, 2); chunk.WriteByte(arrName, 2);
        EmitConstant(chunk, GrobValue.FromFunction(leaf), 2);
        chunk.WriteOpCode(OpCode.DefineGlobal, 2); chunk.WriteByte(leafName, 2);
        chunk.WriteOpCode(OpCode.GetGlobal, 2); chunk.WriteByte(leafName, 2);
        chunk.WriteOpCode(OpCode.Call, 2); chunk.WriteByte(0, 2);
    }

    /// <summary>Emits the try body for <paramref name="route"/>, which faults at the fault position.</summary>
    private static void EmitFault(Chunk chunk, string route) {
        GrobValue array = GrobValue.FromArray(new GrobArray([GrobValue.FromInt(1), GrobValue.FromInt(2), GrobValue.FromInt(3)]));
        switch (route) {
            case "GetIndex out of range":
                EmitConstant(chunk, array, FaultLine);
                EmitConstant(chunk, GrobValue.FromInt(5), FaultLine);
                EmitAtFault(chunk, OpCode.GetIndex);
                break;
            case "SetIndex out of range":
                EmitConstant(chunk, array, FaultLine);
                EmitConstant(chunk, GrobValue.FromInt(5), FaultLine);
                EmitConstant(chunk, GrobValue.FromInt(0), FaultLine);
                EmitAtFault(chunk, OpCode.SetIndex);
                break;
            case "DivideInt by zero":
            case "ModuloInt by zero":
                EmitConstant(chunk, GrobValue.FromInt(10), FaultLine);
                EmitConstant(chunk, GrobValue.FromInt(0), FaultLine);
                EmitAtFault(chunk, route == "DivideInt by zero" ? OpCode.DivideInt : OpCode.ModuloInt);
                break;
            case "DivideFloat by zero":
            case "ModuloFloat by zero":
                EmitConstant(chunk, GrobValue.FromFloat(10.0), FaultLine);
                EmitConstant(chunk, GrobValue.FromFloat(0.0), FaultLine);
                EmitAtFault(chunk, route == "DivideFloat by zero" ? OpCode.DivideFloat : OpCode.ModuloFloat);
                break;
            case "GetIndex on nil":
                chunk.WriteOpCode(OpCode.Nil, FaultLine);
                EmitConstant(chunk, GrobValue.FromInt(0), FaultLine);
                EmitAtFault(chunk, OpCode.GetIndex);
                break;
            case "SetIndex on nil":
                chunk.WriteOpCode(OpCode.Nil, FaultLine);
                EmitConstant(chunk, GrobValue.FromInt(0), FaultLine);
                EmitConstant(chunk, GrobValue.FromInt(1), FaultLine);
                EmitAtFault(chunk, OpCode.SetIndex);
                break;
            case "GetProperty on nil":
                chunk.WriteOpCode(OpCode.Nil, FaultLine);
                EmitAtFault(chunk, OpCode.GetProperty, ConstByte(chunk, GrobValue.FromString("length")));
                break;
            case "AddInt overflow":
                EmitConstant(chunk, GrobValue.FromInt(long.MaxValue), FaultLine);
                EmitConstant(chunk, GrobValue.FromInt(1), FaultLine);
                EmitAtFault(chunk, OpCode.AddInt);
                break;
            case "value stack full":
                // One pool entry pushed again and again: a pool entry per push would pass the
                // one-byte operand's range.
                byte one = ConstByte(chunk, GrobValue.FromInt(1));
                for (int i = 0; i < ValueStack.Capacity + 1; i++) {
                    chunk.WriteOpCode(OpCode.Constant, FaultLine, FaultColumn);
                    chunk.WriteByte(one, FaultLine, FaultColumn);
                }
                break;
            case "Call depth":
                EmitCallDepthFault(chunk);
                break;
            case "Call depth in a callback":
                EmitCallbackDepthFault(chunk);
                break;
            case "callee DivideInt":
                var calleeChunk = new Chunk();
                EmitConstant(calleeChunk, GrobValue.FromInt(10), FaultLine);
                EmitConstant(calleeChunk, GrobValue.FromInt(0), FaultLine);
                EmitAtFault(calleeChunk, OpCode.DivideInt);
                calleeChunk.WriteOpCode(OpCode.Return, FaultLine);
                EmitConstant(chunk, GrobValue.FromFunction(new BytecodeFunction("risky", 0, calleeChunk)), 2);
                chunk.WriteOpCode(OpCode.Call, 2); chunk.WriteByte(0, 2);
                break;
            case "callback DivideInt":
                EmitEach(chunk, BuildDivideByZeroCallback());
                break;
            case "native ParseError E5702":
                EmitConstant(chunk, GrobValue.FromFunction(Faulting("ParseError", ErrorCatalog.E5702.Code)), FaultLine);
                EmitAtFault(chunk, OpCode.Call, 0);
                break;
            case "native IndexError E5102":
                EmitConstant(chunk, GrobValue.FromFunction(Faulting("IndexError", ErrorCatalog.E5102.Code)), FaultLine);
                EmitAtFault(chunk, OpCode.Call, 0);
                break;
            case "native RuntimeError E5903":
                EmitConstant(chunk, GrobValue.FromFunction(Faulting("RuntimeError", ErrorCatalog.E5903.Code)), FaultLine);
                EmitAtFault(chunk, OpCode.Call, 0);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(route), route, "no such fault route");
        }
        chunk.WriteOpCode(OpCode.Pop, FaultLine);
    }

    /// <summary>
    /// Every route to the handler walk, with the code its fault keeps when nothing replaces it
    /// and the column that diagnostic carries. A full value stack is raised by the stack, which
    /// knows the line alone.
    /// </summary>
    public static TheoryData<string, string, int> Routes => new() {
        { "GetIndex out of range", ErrorCatalog.E5101.Code, FaultColumn },
        { "SetIndex out of range", ErrorCatalog.E5101.Code, FaultColumn },
        { "DivideInt by zero", ErrorCatalog.E5002.Code, FaultColumn },
        { "ModuloInt by zero", ErrorCatalog.E5003.Code, FaultColumn },
        { "DivideFloat by zero", ErrorCatalog.E5004.Code, FaultColumn },
        { "ModuloFloat by zero", ErrorCatalog.E5005.Code, FaultColumn },
        { "GetIndex on nil", ErrorCatalog.E5201.Code, FaultColumn },
        { "SetIndex on nil", ErrorCatalog.E5201.Code, FaultColumn },
        { "GetProperty on nil", ErrorCatalog.E5201.Code, FaultColumn },
        { "AddInt overflow", ErrorCatalog.E5001.Code, FaultColumn },
        { "value stack full", ErrorCatalog.E5903.Code, 0 },
        { "Call depth", ErrorCatalog.E5901.Code, FaultColumn },
        { "Call depth in a callback", ErrorCatalog.E5901.Code, FaultColumn },
        { "callee DivideInt", ErrorCatalog.E5002.Code, FaultColumn },
        { "callback DivideInt", ErrorCatalog.E5002.Code, FaultColumn },
        { "native ParseError E5702", ErrorCatalog.E5702.Code, FaultColumn },
        { "native IndexError E5102", ErrorCatalog.E5102.Code, FaultColumn },
        { "native RuntimeError E5903", ErrorCatalog.E5903.Code, FaultColumn },
    };

    /// <summary>
    /// Builds
    /// <code>
    /// try {                                   // only when outerCatch
    ///     try { &lt;route's fault&gt; }
    ///     finally {
    ///         print("finally")
    ///         throw RuntimeError { message: "replaced" }   // only when finallyThrows
    ///     }
    /// } catch e { caught := e }               // only when outerCatch
    /// print("after")
    /// </code>
    /// </summary>
    private static Chunk BuildScript(string route, bool finallyThrows, bool outerCatch) {
        var script = new Chunk();
        byte caughtName = ConstByte(script, GrobValue.FromString("caught"));
        int outerRegion = outerCatch ? script.AddTryRegion() : -1;
        int innerRegion = script.AddTryRegion();

        int outerStart = 0;
        if (outerCatch) {
            script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)outerRegion, 1);
            outerStart = script.Count;
        }

        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)innerRegion, 2);
        int innerStart = script.Count;
        EmitFault(script, route);
        int innerEnd = script.Count;

        int finallyOffset = script.Count;
        EmitPrintString(script, "finally", 4);
        if (finallyThrows) EmitThrowRuntimeError(script, "replaced", ReplaceLine);
        int innerTryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 6);
        script.SetTryRegion(innerRegion, new TryRegion(innerStart, innerEnd, [], finallyOffset,
            BaseSlot: 0, TryEndOffset: innerTryEnd));

        if (outerCatch) {
            int outerEnd = script.Count;
            script.WriteOpCode(OpCode.Jump, 7);
            int jumpSite = script.Count;
            script.WriteByte(0xFF, 7); script.WriteByte(0xFF, 7);

            int handler = script.Count;
            script.WriteOpCode(OpCode.GetLocal, 8); script.WriteByte(0, 8);
            script.WriteOpCode(OpCode.DefineGlobal, 8); script.WriteByte(caughtName, 8);
            script.WriteOpCode(OpCode.Pop, 8);

            PatchJump16(script, jumpSite);
            int outerTryEnd = script.Count;
            script.WriteOpCode(OpCode.TryEnd, 9);
            script.SetTryRegion(outerRegion, new TryRegion(outerStart, outerEnd,
                [new CatchHandler([], IsCatchAll: true, handler, BindingSlot: 0)],
                FinallyOffset: -1, BaseSlot: 0, TryEndOffset: outerTryEnd));
        }

        EmitPrintString(script, "after", 10);
        script.WriteOpCode(OpCode.Return, 10);
        return script;
    }

    /// <summary>
    /// Runs <paramref name="script"/> on a thread with a large stack, since the call-depth route
    /// in a callback nests several host frames per Grob frame, and returns what it threw, or null.
    /// </summary>
    private static Exception? RunCapturing(VirtualMachine vm, Chunk script) {
        Exception? thrown = null;
        var thread = new Thread(() => {
            try {
                vm.Run(script);
            } catch (Exception ex) {
                thrown = ex;
            }
        }, maxStackSize: 32 * 1024 * 1024);
        thread.Start();
        thread.Join();
        return thrown;
    }

    private static string Output(StringWriter output) => output.ToString().ReplaceLineEndings("\n");

    [Theory]
    [MemberData(nameof(Routes))]
    public void Fault_FinallyThrowsAndNothingCatches_ReportsE5904NamingTheReplacementAtTheFault(
            string route, string faultCode, int faultColumn) {
        _ = faultCode;
        var output = new StringWriter();
        var vm = new VirtualMachine(output);

        Exception? thrown = RunCapturing(vm, BuildScript(route, finallyThrows: true, outerCatch: false));

        GrobRuntimeException ex = Assert.IsType<GrobRuntimeException>(thrown);
        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(ReplacedDiagnostic, ex.Message);
        Assert.Equal(FaultLine, ex.Line);
        Assert.Equal(faultColumn, ex.Column);
        Assert.Equal("finally\n", Output(output));
        Assert.Equal(0, vm.FrameCount);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Fault_FinallyThrowsAndAnOuterCatchCatches_TheCatchReceivesTheReplacement(
            string route, string faultCode, int faultColumn) {
        _ = (faultCode, faultColumn);
        var output = new StringWriter();
        var vm = new VirtualMachine(output);

        Exception? thrown = RunCapturing(vm, BuildScript(route, finallyThrows: true, outerCatch: true));

        Assert.Null(thrown);
        Assert.True(vm.Globals["caught"].TryAsStruct(out GrobStruct? caught));
        Assert.Equal("RuntimeError", caught!.TypeName);
        Assert.Equal("replaced", caught.GetField("message").AsString());
        Assert.Equal($"<unknown>:{ReplaceLine}", caught.GetField("location").AsString());
        Assert.Equal("finally\nafter\n", Output(output));
        Assert.Equal(0, vm.FrameCount);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Fault_FinallyDoesNotThrow_TheFaultSurfacesWithItsOwnCodeAndPosition(
            string route, string faultCode, int faultColumn) {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);

        Exception? thrown = RunCapturing(vm, BuildScript(route, finallyThrows: false, outerCatch: false));

        GrobRuntimeException ex = Assert.IsAssignableFrom<GrobRuntimeException>(thrown);
        Assert.Equal(faultCode, ex.Code);
        Assert.Equal(FaultLine, ex.Line);
        Assert.Equal(faultColumn, ex.Column);
        Assert.Equal("finally\n", Output(output));
    }

    // -----------------------------------------------------------------------
    // Shapes beside the routes
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>try { try { 10 / 0 } finally { print("inner"); [throw A] } } finally { print("outer"); [throw B] }</c>.
    /// </summary>
    private static Chunk BuildNestedFinallies(string? innerThrows, string? outerThrows) {
        var script = new Chunk();
        int outerRegion = script.AddTryRegion();
        int innerRegion = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)outerRegion, 1);
        int outerStart = script.Count;
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)innerRegion, 2);
        int innerStart = script.Count;
        EmitFault(script, "DivideInt by zero");
        int innerEnd = script.Count;

        int innerFinally = script.Count;
        EmitPrintString(script, "inner", 4);
        if (innerThrows is not null) EmitThrowRuntimeError(script, innerThrows, ReplaceLine);
        int innerTryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 6);
        int outerEnd = script.Count;

        int outerFinally = script.Count;
        EmitPrintString(script, "outer", 7);
        if (outerThrows is not null) EmitThrowRuntimeError(script, outerThrows, 8);
        int outerTryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 9);
        script.WriteOpCode(OpCode.Return, 9);

        script.SetTryRegion(innerRegion, new TryRegion(innerStart, innerEnd, [], innerFinally,
            BaseSlot: 0, TryEndOffset: innerTryEnd));
        script.SetTryRegion(outerRegion, new TryRegion(outerStart, outerEnd, [], outerFinally,
            BaseSlot: 0, TryEndOffset: outerTryEnd));
        return script;
    }

    [Theory]
    [InlineData(null, "outer replaced", "RuntimeError: outer replaced")]
    [InlineData("inner replaced", null, "RuntimeError: inner replaced")]
    [InlineData("inner replaced", "outer replaced", "RuntimeError: outer replaced")]
    public void Fault_NestedFinallies_TheLastReplacementIsReported(
            string? innerThrows, string? outerThrows, string expectedMessage) {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildNestedFinallies(innerThrows, outerThrows)));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(expectedMessage, ex.Message);
        Assert.Equal(FaultLine, ex.Line);
        Assert.Equal(FaultColumn, ex.Column);
        Assert.Equal("inner\nouter\n", Output(output));
    }

    [Fact]
    public void Fault_NestedFinalliesNeitherThrows_TheFaultSurfacesAfterBoth() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);

        GrobArithmeticException ex = Assert.Throws<GrobArithmeticException>(
            () => vm.Run(BuildNestedFinallies(null, null)));

        Assert.Equal(ErrorCatalog.E5002.Code, ex.Code);
        Assert.Equal(FaultLine, ex.Line);
        Assert.Equal(FaultColumn, ex.Column);
        Assert.Equal("inner\nouter\n", Output(output));
    }

    /// <summary>
    /// <c>x =&gt; { try { x / 0 } finally { throw RuntimeError { message: "replaced" } } }</c>:
    /// the finally is the callback's own, so the replacement leaves the callback.
    /// </summary>
    private static BytecodeFunction BuildCallbackWithReplacingFinally() {
        var fnChunk = new Chunk();
        int region = fnChunk.AddTryRegion();
        fnChunk.WriteOpCode(OpCode.TryBegin, 2); fnChunk.WriteByte((byte)region, 2);
        int start = fnChunk.Count;
        fnChunk.WriteOpCode(OpCode.GetLocal, FaultLine); fnChunk.WriteByte(0, FaultLine);
        EmitConstant(fnChunk, GrobValue.FromInt(0), FaultLine);
        EmitAtFault(fnChunk, OpCode.DivideInt);
        fnChunk.WriteOpCode(OpCode.Pop, FaultLine);
        int end = fnChunk.Count;
        int finallyOffset = fnChunk.Count;
        EmitThrowRuntimeError(fnChunk, "replaced", ReplaceLine);
        int tryEnd = fnChunk.Count;
        fnChunk.WriteOpCode(OpCode.TryEnd, 6);
        fnChunk.WriteOpCode(OpCode.Nil, 6);
        fnChunk.WriteOpCode(OpCode.Return, 6);
        fnChunk.SetTryRegion(region, new TryRegion(start, end, [], finallyOffset, BaseSlot: 1, TryEndOffset: tryEnd));
        return new BytecodeFunction("", 1, fnChunk);
    }

    [Fact]
    public void CallbackFault_ReplacedByTheCallbacksOwnFinally_Uncaught_ReportsE5904AtTheFault() {
        var script = new Chunk();
        EmitEach(script, BuildCallbackWithReplacingFinally());
        script.WriteOpCode(OpCode.Pop, 2);
        script.WriteOpCode(OpCode.Return, 2);
        var vm = new VirtualMachine(new StringWriter());

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(ReplacedDiagnostic, ex.Message);
        Assert.Equal(FaultLine, ex.Line);
        Assert.Equal(FaultColumn, ex.Column);
        Assert.Equal(0, vm.FrameCount);
    }

    [Fact]
    public void CalleeFault_ReplacedByTheCalleesOwnFinally_Uncaught_ReportsE5904AtTheFault() {
        // fn risky() { try { 10 / 0 } finally { throw RuntimeError { message: "replaced" } } }
        // risky()
        var calleeChunk = new Chunk();
        int region = calleeChunk.AddTryRegion();
        calleeChunk.WriteOpCode(OpCode.TryBegin, 2); calleeChunk.WriteByte((byte)region, 2);
        int start = calleeChunk.Count;
        EmitFault(calleeChunk, "DivideInt by zero");
        int end = calleeChunk.Count;
        int finallyOffset = calleeChunk.Count;
        EmitThrowRuntimeError(calleeChunk, "replaced", ReplaceLine);
        int tryEnd = calleeChunk.Count;
        calleeChunk.WriteOpCode(OpCode.TryEnd, 6);
        calleeChunk.WriteOpCode(OpCode.Nil, 6);
        calleeChunk.WriteOpCode(OpCode.Return, 6);
        calleeChunk.SetTryRegion(region, new TryRegion(start, end, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEnd));

        var script = new Chunk();
        EmitConstant(script, GrobValue.FromFunction(new BytecodeFunction("risky", 0, calleeChunk)), 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Pop, 1);
        script.WriteOpCode(OpCode.Return, 1);
        var vm = new VirtualMachine(new StringWriter());

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(ReplacedDiagnostic, ex.Message);
        Assert.Equal(FaultLine, ex.Line);
        Assert.Equal(FaultColumn, ex.Column);
        Assert.Equal(0, vm.FrameCount);
    }

    [Fact]
    public void HostFault_FinallyThatWouldReplaceIt_IsSkipped() {
        // D-459 Decision 6: an internal error ends the run at once, so not even a finally that
        // throws runs; the report is the internal error, not a replacement.
        var script = new Chunk();
        int region = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)region, 2);
        int start = script.Count;
        var host = new NativeFunction("faulty", 0, (_, _) => throw new InvalidOperationException("boom"));
        EmitConstant(script, GrobValue.FromFunction(host), FaultLine);
        EmitAtFault(script, OpCode.Call, 0);
        script.WriteOpCode(OpCode.Pop, FaultLine);
        int end = script.Count;
        int finallyOffset = script.Count;
        EmitPrintString(script, "finally", 4);
        EmitThrowRuntimeError(script, "replaced", ReplaceLine);
        int tryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 6);
        script.WriteOpCode(OpCode.Return, 6);
        script.SetTryRegion(region, new TryRegion(start, end, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEnd));
        var output = new StringWriter();
        var vm = new VirtualMachine(output);

        GrobInternalException ex = Assert.Throws<GrobInternalException>(() => vm.Run(script));

        Assert.Contains("native faulty threw InvalidOperationException", ex.Message, StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void ScriptThrow_FinallyThrowsAndNothingCatches_ReportsE5904NamingTheReplacementAtTheFirstThrow() {
        // The control the fault routes are made to match: a script throw a finally replaces.
        var script = new Chunk();
        int region = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)region, 2);
        int start = script.Count;
        byte type = script.AddStructType(new StructTypeDescriptor("RuntimeError", ["message"]));
        EmitConstant(script, GrobValue.FromString("original"), FaultLine);
        script.WriteOpCode(OpCode.NewStruct, FaultLine); script.WriteByte(type, FaultLine);
        EmitAtFault(script, OpCode.Throw);
        int end = script.Count;
        int finallyOffset = script.Count;
        EmitThrowRuntimeError(script, "replaced", ReplaceLine);
        int tryEnd = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 6);
        script.WriteOpCode(OpCode.Return, 6);
        script.SetTryRegion(region, new TryRegion(start, end, [], finallyOffset, BaseSlot: 0, TryEndOffset: tryEnd));
        var vm = new VirtualMachine(new StringWriter());

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(ReplacedDiagnostic, ex.Message);
        Assert.Equal(FaultLine, ex.Line);
        Assert.Equal(FaultColumn, ex.Column);
    }
}
