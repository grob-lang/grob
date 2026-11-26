using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM tests for an error raised in a callback that a native runs (R-91, D-458). The native
/// runs its callback in a re-entrant dispatch; an error the callback does not catch leaves
/// that dispatch at its floor frame, unwinds through the native and is routed again from
/// the native's call site by the dispatch that made the call. The script's handler then
/// runs in the script's own dispatch, the native's loop goes no further and the stack is
/// as the call site left it. All chunks are hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineCallbackThrowTests {
    /// <summary>Line of every callback's faulting instruction, so a diagnostic's line is checkable.</summary>
    private const int FaultLine = 7;

    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) =>
        (byte)chunk.AddConstant(value);

    /// <summary>Backpatches a 2-byte forward jump written as two 0xFF placeholder bytes.</summary>
    private static void PatchJump16(Chunk chunk, int patchSite) {
        int offset = chunk.Count - (patchSite + 2);
        chunk.PatchByte(patchSite, (byte)(offset >> 8));
        chunk.PatchByte(patchSite + 1, (byte)(offset & 0xFF));
    }

    private static void EmitPrintString(Chunk chunk, string text, int line) {
        chunk.WriteOpCode(OpCode.Constant, line);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(text)), line);
        chunk.WriteOpCode(OpCode.Print, line);
    }

    /// <summary>
    /// <c>x =&gt; { print(x); x / 0 }</c>: prints its argument, so a test can see how many
    /// times the native ran it, then divides by zero on <see cref="FaultLine"/>.
    /// </summary>
    private static BytecodeFunction BuildPrintThenDivideByZeroLambda() {
        var fnChunk = new Chunk();
        fnChunk.WriteOpCode(OpCode.GetLocal, 6); fnChunk.WriteByte(0, 6);
        fnChunk.WriteOpCode(OpCode.Print, 6);
        fnChunk.WriteOpCode(OpCode.GetLocal, FaultLine); fnChunk.WriteByte(0, FaultLine);
        fnChunk.WriteOpCode(OpCode.Constant, FaultLine);
        fnChunk.WriteByte(ConstByte(fnChunk, GrobValue.FromInt(0)), FaultLine);
        fnChunk.WriteOpCode(OpCode.DivideInt, FaultLine);
        fnChunk.WriteOpCode(OpCode.Return, FaultLine);
        return new BytecodeFunction("", 1, fnChunk);
    }

    /// <summary><c>p =&gt; p + 9223372036854775807</c>, the register row's reproducer.</summary>
    private static BytecodeFunction BuildOverflowLambda() {
        var fnChunk = new Chunk();
        fnChunk.WriteOpCode(OpCode.GetLocal, FaultLine); fnChunk.WriteByte(0, FaultLine);
        fnChunk.WriteOpCode(OpCode.Constant, FaultLine);
        fnChunk.WriteByte(ConstByte(fnChunk, GrobValue.FromInt(long.MaxValue)), FaultLine);
        fnChunk.WriteOpCode(OpCode.AddInt, FaultLine);
        fnChunk.WriteOpCode(OpCode.Return, FaultLine);
        return new BytecodeFunction("", 1, fnChunk);
    }

    /// <summary>Emits <c>[1, 2].&lt;method&gt;(callback)</c>, leaving the result on the stack.</summary>
    private static void EmitArrayMethodCall(Chunk chunk, string method, BytecodeFunction callback, int line) {
        GrobValue array = GrobValue.FromArray(new GrobArray([GrobValue.FromInt(1), GrobValue.FromInt(2)]));
        chunk.WriteOpCode(OpCode.Constant, line); chunk.WriteByte(ConstByte(chunk, array), line);
        chunk.WriteOpCode(OpCode.GetProperty, line);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(method)), line);
        chunk.WriteOpCode(OpCode.Constant, line);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromFunction(callback)), line);
        chunk.WriteOpCode(OpCode.Call, line); chunk.WriteByte(1, line);
    }

    /// <summary>
    /// Builds the script
    /// <code>
    /// keep := 42                                  // slot 0, read after the try
    /// try { [1, 2].&lt;method&gt;(callback) } catch (e: ArithmeticError) { print("caught") }
    /// print(keep)
    /// print("after")
    /// </code>
    /// with the catch binding at slot 1, the try's base slot.
    /// </summary>
    private static Chunk BuildCaughtCallbackScript(string method, BytecodeFunction callback) {
        var script = new Chunk();
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte(ConstByte(script, GrobValue.FromInt(42)), 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;
        EmitArrayMethodCall(script, method, callback, 2);
        script.WriteOpCode(OpCode.Pop, 2);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count;
        EmitPrintString(script, "caught", 3);
        script.WriteOpCode(OpCode.Pop, 3); // the catch binding leaves scope

        PatchJump16(script, jumpSite);
        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 3);
        script.WriteOpCode(OpCode.GetLocal, 4); script.WriteByte(0, 4);
        script.WriteOpCode(OpCode.Print, 4);
        EmitPrintString(script, "after", 5);
        script.WriteOpCode(OpCode.Return, 5);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["ArithmeticError"], IsCatchAll: false, handlerOffset, BindingSlot: 1)],
            FinallyOffset: -1, BaseSlot: 1, TryEndOffset: tryEndOffset));
        return script;
    }

    /// <summary>The script ran to its end with its own local intact and no frame left.</summary>
    private static void AssertRanToEnd(VirtualMachine vm) {
        Assert.Equal(0, vm.FrameCount);
        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(42L, vm.Stack.GetSlot(0).AsInt());
    }

    [Theory]
    [InlineData("select")]
    [InlineData("filter")]
    [InlineData("sort")]
    [InlineData("each")]
    public void CallbackError_CaughtByScriptTry_ScriptRunsToEndWithStackIntact(string method) {
        var (vm, output) = NewVm();

        vm.Run(BuildCaughtCallbackScript(method, BuildPrintThenDivideByZeroLambda()));

        // "1" once: the native's loop stops at the first callback's error.
        Assert.Equal("1\ncaught\n42\nafter\n", output.ToString().ReplaceLineEndings("\n"));
        AssertRanToEnd(vm);
    }

    [Fact]
    public void CallbackOverflow_InSelect_CaughtByScriptTry_ScriptRunsToEnd() {
        var (vm, output) = NewVm();

        vm.Run(BuildCaughtCallbackScript("select", BuildOverflowLambda()));

        Assert.Equal("caught\n42\nafter\n", output.ToString().ReplaceLineEndings("\n"));
        AssertRanToEnd(vm);
    }

    [Fact]
    public void CallbackError_InNativeRunFromCallback_CaughtByScriptTry_ScriptRunsToEnd() {
        // x => [x].each(inner), with inner the print-then-divide lambda: two re-entrant
        // dispatches between the error and the script's handler.
        var outerChunk = new Chunk();
        outerChunk.WriteOpCode(OpCode.GetLocal, 6); outerChunk.WriteByte(0, 6);
        outerChunk.WriteOpCode(OpCode.NewArray, 6); outerChunk.WriteByte(1, 6);
        outerChunk.WriteOpCode(OpCode.GetProperty, 6);
        outerChunk.WriteByte(ConstByte(outerChunk, GrobValue.FromString("each")), 6);
        outerChunk.WriteOpCode(OpCode.Constant, 6);
        outerChunk.WriteByte(ConstByte(outerChunk, GrobValue.FromFunction(BuildPrintThenDivideByZeroLambda())), 6);
        outerChunk.WriteOpCode(OpCode.Call, 6); outerChunk.WriteByte(1, 6);
        outerChunk.WriteOpCode(OpCode.Return, 6);
        var outer = new BytecodeFunction("", 1, outerChunk);
        var (vm, output) = NewVm();

        vm.Run(BuildCaughtCallbackScript("select", outer));

        Assert.Equal("1\ncaught\n42\nafter\n", output.ToString().ReplaceLineEndings("\n"));
        AssertRanToEnd(vm);
    }

    [Fact]
    public void CallbackError_InFunctionTheCallbackCalls_CaughtByScriptTry_ScriptRunsToEnd() {
        // x => helper(x), with helper the print-then-divide body as a named function: the
        // error unwinds the helper's frame before it reaches the callback's floor.
        var callbackChunk = new Chunk();
        BytecodeFunction helper = BuildPrintThenDivideByZeroLambda();
        callbackChunk.WriteOpCode(OpCode.Constant, 6);
        callbackChunk.WriteByte(ConstByte(callbackChunk, GrobValue.FromFunction(helper)), 6);
        callbackChunk.WriteOpCode(OpCode.GetLocal, 6); callbackChunk.WriteByte(0, 6);
        callbackChunk.WriteOpCode(OpCode.Call, 6); callbackChunk.WriteByte(1, 6);
        callbackChunk.WriteOpCode(OpCode.Return, 6);
        var callback = new BytecodeFunction("", 1, callbackChunk);
        var (vm, output) = NewVm();

        vm.Run(BuildCaughtCallbackScript("each", callback));

        Assert.Equal("1\ncaught\n42\nafter\n", output.ToString().ReplaceLineEndings("\n"));
        AssertRanToEnd(vm);
    }

    [Fact]
    public void CallbackError_CaughtInsideCallback_NativeLoopContinues() {
        // x => { try { print(x); x / 0 } catch (e: ArithmeticError) { print("inner") }; 0 }
        var fnChunk = new Chunk();
        int regionIndex = fnChunk.AddTryRegion();
        fnChunk.WriteOpCode(OpCode.TryBegin, 6); fnChunk.WriteByte((byte)regionIndex, 6);
        int startOffset = fnChunk.Count;
        fnChunk.WriteOpCode(OpCode.GetLocal, 6); fnChunk.WriteByte(0, 6);
        fnChunk.WriteOpCode(OpCode.Print, 6);
        fnChunk.WriteOpCode(OpCode.GetLocal, FaultLine); fnChunk.WriteByte(0, FaultLine);
        fnChunk.WriteOpCode(OpCode.Constant, FaultLine);
        fnChunk.WriteByte(ConstByte(fnChunk, GrobValue.FromInt(0)), FaultLine);
        fnChunk.WriteOpCode(OpCode.DivideInt, FaultLine);
        fnChunk.WriteOpCode(OpCode.Pop, FaultLine);
        int endOffset = fnChunk.Count;
        fnChunk.WriteOpCode(OpCode.Jump, 8);
        int jumpSite = fnChunk.Count;
        fnChunk.WriteByte(0xFF, 8); fnChunk.WriteByte(0xFF, 8);
        int handlerOffset = fnChunk.Count;
        EmitPrintString(fnChunk, "inner", 8);
        fnChunk.WriteOpCode(OpCode.Pop, 8);
        PatchJump16(fnChunk, jumpSite);
        int tryEndOffset = fnChunk.Count;
        fnChunk.WriteOpCode(OpCode.TryEnd, 8);
        fnChunk.WriteOpCode(OpCode.Constant, 9);
        fnChunk.WriteByte(ConstByte(fnChunk, GrobValue.FromInt(0)), 9);
        fnChunk.WriteOpCode(OpCode.Return, 9);
        fnChunk.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["ArithmeticError"], IsCatchAll: false, handlerOffset, BindingSlot: 1)],
            FinallyOffset: -1, BaseSlot: 1, TryEndOffset: tryEndOffset));
        var (vm, output) = NewVm();

        vm.Run(BuildCaughtCallbackScript("each", new BytecodeFunction("", 1, fnChunk)));

        // Both elements run and the script's own catch never does.
        Assert.Equal("1\ninner\n2\ninner\n42\nafter\n", output.ToString().ReplaceLineEndings("\n"));
        AssertRanToEnd(vm);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CallbackThrow_UncaughtWithNoStringMessage_RaisesE5904WithPlaceholder(bool hasIntMessage) {
        // A thrown error whose message field is missing, or not a string, is named with a
        // placeholder in the E5904 raised once the error has left the callback.
        GrobStruct error = hasIntMessage
            ? new GrobStruct("RuntimeError", [new KeyValuePair<string, GrobValue>("message", GrobValue.FromInt(3))])
            : new GrobStruct("RuntimeError", []);
        var fnChunk = new Chunk();
        fnChunk.WriteOpCode(OpCode.Constant, FaultLine);
        fnChunk.WriteByte(ConstByte(fnChunk, GrobValue.FromStruct(error)), FaultLine);
        fnChunk.WriteOpCode(OpCode.Throw, FaultLine);
        var script = new Chunk();
        EmitArrayMethodCall(script, "each", new BytecodeFunction("", 1, fnChunk), 2);
        script.WriteOpCode(OpCode.Return, 2);
        var (vm, _) = NewVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(FaultLine, ex.Line);
        Assert.Equal("RuntimeError: <no message>", ex.Message);
    }

    [Theory]
    [InlineData("select")]
    [InlineData("filter")]
    [InlineData("sort")]
    [InlineData("each")]
    public void CallbackError_Uncaught_RaisesOriginalDiagnosticAtCallbackLine(string method) {
        var script = new Chunk();
        EmitArrayMethodCall(script, method, BuildPrintThenDivideByZeroLambda(), 2);
        script.WriteOpCode(OpCode.Pop, 2);
        EmitPrintString(script, "after", 3);
        script.WriteOpCode(OpCode.Return, 3);
        var (vm, output) = NewVm();

        GrobArithmeticException ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5002.Code, ex.Code);
        Assert.Equal(FaultLine, ex.Line);
        Assert.Equal("1\n", output.ToString().ReplaceLineEndings("\n"));
    }
}
