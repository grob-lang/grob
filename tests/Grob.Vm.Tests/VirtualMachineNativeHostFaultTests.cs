using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM tests for an exception a native throws that is not a Grob error (R-69, D-459). A native
/// signals a script's bad input with <see cref="NativeFaultException"/>; any other exception is
/// a host bug, reported as a <see cref="GrobInternalException"/> (E9001 at the CLI) that names
/// the native and the call's source position and that a script <c>catch</c> cannot swallow.
/// The VM's own control exceptions and the errors it already routes pass through untouched.
/// All chunks are hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineNativeHostFaultTests {
    private const int CallLine = 5;
    private const int CallColumn = 9;

    private static NativeFunction Throwing(string name, Exception toThrow) =>
        new(name, 0, (_, _) => throw toThrow);

    private static byte ConstByte(Chunk chunk, GrobValue value) => (byte)chunk.AddConstant(value);

    private static void PatchJump16(Chunk chunk, int patchSite) {
        int offset = chunk.Count - (patchSite + 2);
        chunk.PatchByte(patchSite, (byte)(offset >> 8));
        chunk.PatchByte(patchSite + 1, (byte)(offset & 0xFF));
    }

    /// <summary>Emits <c>native(args)</c> as a call at <see cref="CallLine"/>:<see cref="CallColumn"/>.</summary>
    private static void EmitCall(Chunk chunk, NativeFunction native, params GrobValue[] args) {
        chunk.WriteOpCode(OpCode.Constant, CallLine, CallColumn);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromFunction(native)), CallLine, CallColumn);
        foreach (GrobValue arg in args) {
            chunk.WriteOpCode(OpCode.Constant, CallLine, CallColumn);
            chunk.WriteByte(ConstByte(chunk, arg), CallLine, CallColumn);
        }
        chunk.WriteOpCode(OpCode.Call, CallLine, CallColumn);
        chunk.WriteByte((byte)args.Length, CallLine, CallColumn);
    }

    private static Chunk BuildCallScript(NativeFunction native, params GrobValue[] args) {
        var script = new Chunk();
        EmitCall(script, native, args);
        script.WriteOpCode(OpCode.Return, CallLine);
        return script;
    }

    /// <summary>
    /// <c>try { native() } catch { print("caught") }</c> followed by <c>print("after")</c>, the
    /// handler a catch-all.
    /// </summary>
    private static Chunk BuildCaughtCallScript(NativeFunction native) {
        var script = new Chunk();
        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;
        EmitCall(script, native);
        script.WriteOpCode(OpCode.Pop, CallLine);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, CallLine);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, CallLine); script.WriteByte(0xFF, CallLine);

        int handlerOffset = script.Count;
        script.WriteOpCode(OpCode.Constant, 6); script.WriteByte(ConstByte(script, GrobValue.FromString("caught")), 6);
        script.WriteOpCode(OpCode.Print, 6);
        script.WriteOpCode(OpCode.Pop, 6);

        PatchJump16(script, jumpSite);
        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 7);
        script.WriteOpCode(OpCode.Constant, 7); script.WriteByte(ConstByte(script, GrobValue.FromString("after")), 7);
        script.WriteOpCode(OpCode.Print, 7);
        script.WriteOpCode(OpCode.Return, 7);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler([], IsCatchAll: true, handlerOffset, BindingSlot: 0)],
            FinallyOffset: -1, BaseSlot: 0, TryEndOffset: tryEndOffset));
        return script;
    }

    /// <summary><c>x =&gt; native()</c>: a callback that calls the native at its own line and column.</summary>
    private static BytecodeFunction BuildCallbackCalling(NativeFunction native) {
        var fnChunk = new Chunk();
        EmitCall(fnChunk, native);
        fnChunk.WriteOpCode(OpCode.Return, CallLine);
        return new BytecodeFunction("", 1, fnChunk);
    }

    private static GrobInternalException RunExpectingInternal(Chunk script, VirtualMachine vm) =>
        Assert.Throws<GrobInternalException>(() => vm.Run(script));

    private static void AssertNamesNativeAndCallSite(
            GrobInternalException ex, string native, string hostType, Exception original) {
        Assert.Contains($"native {native} threw {hostType}", ex.Message, StringComparison.Ordinal);
        Assert.Contains($"line {CallLine}, column {CallColumn}", ex.Message, StringComparison.Ordinal);
        Assert.Same(original, ex.InnerException);
    }

    // -----------------------------------------------------------------------
    // The net
    // -----------------------------------------------------------------------

    [Fact]
    public void HostException_FromNative_IsInternalErrorNamingNativeAndCallSite() {
        var original = new InvalidOperationException("secret script value");
        var vm = new VirtualMachine(new StringWriter());

        GrobInternalException ex = RunExpectingInternal(BuildCallScript(Throwing("boom", original)), vm);

        AssertNamesNativeAndCallSite(ex, "boom", nameof(InvalidOperationException), original);
    }

    [Fact]
    public void HostException_FromNative_KeepsTheHostMessageOutOfTheReport() {
        // D-077: a host exception's message can carry a script value, so only its type is named.
        var vm = new VirtualMachine(new StringWriter());

        GrobInternalException ex = RunExpectingInternal(
            BuildCallScript(Throwing("boom", new InvalidOperationException("secret script value"))), vm);

        Assert.DoesNotContain("secret script value", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HostException_FromNative_WithNoRecordedColumn_NamesTheLineAlone() {
        var script = new Chunk();
        script.WriteOpCode(OpCode.Constant, 4);
        script.WriteByte(ConstByte(script, GrobValue.FromFunction(Throwing("boom", new InvalidOperationException()))), 4);
        script.WriteOpCode(OpCode.Call, 4); script.WriteByte(0, 4);
        script.WriteOpCode(OpCode.Return, 4);
        var vm = new VirtualMachine(new StringWriter());

        GrobInternalException ex = RunExpectingInternal(script, vm);

        Assert.Contains("at line 4.", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("column", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HostException_FromNative_IsNotCaughtByAScriptCatchAll() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);

        RunExpectingInternal(BuildCaughtCallScript(Throwing("boom", new InvalidOperationException())), vm);

        // Neither the handler nor the code after the try ran.
        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void HostException_FromNative_LeavesTheVmUsableForTheNextRun() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        RunExpectingInternal(BuildCallScript(Throwing("boom", new InvalidOperationException())), vm);

        // The call popped its callee and arguments before the native ran.
        Assert.Equal(0, vm.Stack.Count);
        Assert.Equal(0, vm.FrameCount);

        vm.Run(BuildCallScript(new NativeFunction("ok", 0, (_, _) => GrobValue.FromInt(7))));

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(7L, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void HostException_FromNativeCalledInACallback_NamesTheNativeAtItsOwnCallSite() {
        // [1].each(x => boom()): the native the callback calls is dispatched by the Call arm of
        // the callback's re-entrant dispatch, and its report is not rewrapped on the way out.
        var original = new InvalidOperationException();
        var vm = new VirtualMachine(new StringWriter());
        var script = new Chunk();
        script.WriteOpCode(OpCode.Constant, 3);
        script.WriteByte(ConstByte(script, GrobValue.FromArray(new GrobArray([GrobValue.FromInt(1)]))), 3);
        script.WriteOpCode(OpCode.GetProperty, 3);
        script.WriteByte(ConstByte(script, GrobValue.FromString("each")), 3);
        script.WriteOpCode(OpCode.Constant, 3);
        script.WriteByte(ConstByte(script, GrobValue.FromFunction(BuildCallbackCalling(Throwing("boom", original)))), 3);
        script.WriteOpCode(OpCode.Call, 3); script.WriteByte(1, 3);
        script.WriteOpCode(OpCode.Return, 3);

        GrobInternalException ex = RunExpectingInternal(script, vm);

        AssertNamesNativeAndCallSite(ex, "boom", nameof(InvalidOperationException), original);
    }

    [Fact]
    public void HostException_FromNativeThatIsItselfTheCallback_NamesTheInnerNativeAtTheOriginalCallSite() {
        // apply(boom): apply runs boom through InvokeCallable's nested-native path. The inner
        // native is the one at fault, and the position is the original Call opcode's (D-397).
        var original = new InvalidOperationException();
        var vm = new VirtualMachine(new StringWriter());
        var apply = new NativeFunction("apply", 1, (args, invoker) => invoker.Invoke(args[0], []));

        GrobInternalException ex = RunExpectingInternal(
            BuildCallScript(apply, GrobValue.FromFunction(Throwing("boom", original))), vm);

        AssertNamesNativeAndCallSite(ex, "boom", nameof(InvalidOperationException), original);
        Assert.DoesNotContain("native apply", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HostException_FromBuiltInArrayNativeCallback_IsInternalErrorNamingTheCallback() {
        // filter's predicate is a native that throws: the nested path again, through a built-in.
        var original = new FormatException();
        var vm = new VirtualMachine(new StringWriter());
        var script = new Chunk();
        script.WriteOpCode(OpCode.Constant, CallLine, CallColumn);
        script.WriteByte(ConstByte(script, GrobValue.FromArray(new GrobArray([GrobValue.FromInt(1)]))), CallLine, CallColumn);
        script.WriteOpCode(OpCode.GetProperty, CallLine, CallColumn);
        script.WriteByte(ConstByte(script, GrobValue.FromString("filter")), CallLine, CallColumn);
        script.WriteOpCode(OpCode.Constant, CallLine, CallColumn);
        script.WriteByte(ConstByte(script, GrobValue.FromFunction(
            new NativeFunction("predicate", 1, (_, _) => throw original))), CallLine, CallColumn);
        script.WriteOpCode(OpCode.Call, CallLine, CallColumn); script.WriteByte(1, CallLine, CallColumn);
        script.WriteOpCode(OpCode.Return, CallLine);

        GrobInternalException ex = RunExpectingInternal(script, vm);

        AssertNamesNativeAndCallSite(ex, "predicate", nameof(FormatException), original);
    }

    // -----------------------------------------------------------------------
    // What the net lets through
    // -----------------------------------------------------------------------

    [Fact]
    public void NativeFault_FromNative_StillRoutesToTheScriptsCatch() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        var fault = new NativeFaultException("RuntimeError", ErrorCatalog.E5903.Code, "bad input");

        vm.Run(BuildCaughtCallScript(Throwing("faulty", fault)));

        Assert.Equal("caught\nafter\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void NativeFault_FromNative_UncaughtKeepsItsCodeAndTheCallLine() {
        var vm = new VirtualMachine(new StringWriter());
        var fault = new NativeFaultException("RuntimeError", ErrorCatalog.E5903.Code, "bad input");

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildCallScript(Throwing("faulty", fault))));

        Assert.Equal(ErrorCatalog.E5903.Code, ex.Code);
        Assert.Equal(CallLine, ex.Line);
        Assert.Equal(CallColumn, ex.Column);
    }

    /// <summary><c>try { native() } finally { print("done") }</c>, the native's error uncaught.</summary>
    private static Chunk BuildFinallyAroundCallScript(NativeFunction native) {
        var script = new Chunk();
        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;
        EmitCall(script, native);
        script.WriteOpCode(OpCode.Pop, CallLine);
        int endOffset = script.Count;

        int finallyOffset = script.Count;
        script.WriteOpCode(OpCode.Constant, 6); script.WriteByte(ConstByte(script, GrobValue.FromString("done")), 6);
        script.WriteOpCode(OpCode.Print, 6);
        int tryEndOffset = script.Count;
        script.WriteOpCode(OpCode.TryEnd, 7);
        script.WriteOpCode(OpCode.Return, 7);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [], finallyOffset,
            BaseSlot: 0, TryEndOffset: tryEndOffset));
        return script;
    }

    [Theory]
    [InlineData("RuntimeError", "E5903")]
    [InlineData("ParseError", "E5702")]
    [InlineData("IndexError", "E5102")]
    public void NativeFault_FromNative_UncaughtRunsAnEnclosingFinallyOnce(string leaf, string code) {
        // E5903 is also the code the dispatch loop intercepts to route a full value stack; a native's
        // own E5903 has already been walked by then and must not be walked a second time (D-459).
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        var fault = new NativeFaultException(leaf, code, "bad input");

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildFinallyAroundCallScript(Throwing("faulty", fault))));

        Assert.Equal(code, ex.Code);
        Assert.Equal(CallLine, ex.Line);
        Assert.Equal(CallColumn, ex.Column);
        Assert.Equal("done\n", output.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void OverflowException_FromNative_IsStillIntegerOverflow() {
        // NumericMethodsPlugin's checked arithmetic relies on the dispatch's own catch (D-382).
        var vm = new VirtualMachine(new StringWriter());

        GrobRuntimeException ex = Assert.Throws<GrobArithmeticException>(
            () => vm.Run(BuildCallScript(Throwing("checked", new OverflowException()))));

        Assert.Equal(ErrorCatalog.E5001.Code, ex.Code);
        Assert.Equal(CallLine, ex.Line);
        Assert.Equal(CallColumn, ex.Column);
    }

    [Fact]
    public void GrobRuntimeException_FromNative_IsNotRewrapped() {
        var vm = new VirtualMachine(new StringWriter());
        var raised = new GrobRuntimeException(ErrorCatalog.E5903.Code, 3, 1, "stack exhausted");

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildCallScript(Throwing("raiser", raised))));

        Assert.Same(raised, ex);
    }

    [Fact]
    public void GrobInternalException_FromNative_IsNotRewrapped() {
        var vm = new VirtualMachine(new StringWriter());
        var internalError = new GrobInternalException("GrobValue kind mismatch");

        GrobInternalException ex = RunExpectingInternal(BuildCallScript(Throwing("broken", internalError)), vm);

        Assert.Same(internalError, ex);
    }

    [Fact]
    public void GrobExitException_FromNative_StillEndsTheRun() {
        var vm = new VirtualMachine(new StringWriter());

        GrobExitException ex = Assert.Throws<GrobExitException>(
            () => vm.Run(BuildCallScript(Throwing("exit", new GrobExitException(3)))));

        Assert.Equal(3, ex.Code);
    }

    [Fact]
    public void OperationCanceledException_FromNative_IsNotAHostFault() {
        // D-319: cancellation is outside every Grob error type and must reach the host as itself.
        var vm = new VirtualMachine(new StringWriter());

        Assert.Throws<OperationCanceledException>(
            () => vm.Run(BuildCallScript(Throwing("slow", new OperationCanceledException()))));
    }

    [Fact]
    public void TaskCanceledException_FromNative_IsNotAHostFault() {
        // TaskCanceledException derives from OperationCanceledException, so a native that awaits
        // a cancelled task is cancellation, not a defect.
        var vm = new VirtualMachine(new StringWriter());

        Assert.Throws<TaskCanceledException>(
            () => vm.Run(BuildCallScript(Throwing("slow", new TaskCanceledException()))));
    }
}
