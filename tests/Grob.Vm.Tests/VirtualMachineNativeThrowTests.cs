using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 8 Increment A's native-throw seam (D-342): a
/// <see cref="NativeFunction"/> implementation that throws
/// <see cref="NativeFaultException"/> is routed through the SAME
/// <c>TryRaiseRuntimeGrobError</c> handler-table walk (D-334) a VM-internal fault
/// (int division by zero etc.) already uses — catchable in <c>try</c>/<c>catch</c>,
/// runs <c>finally</c> exactly once, and produces the same top-level diagnostic quality
/// when unhandled. All chunks are hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineNativeThrowTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static void PatchJump16(Chunk chunk, int patchSite) {
        int offset = chunk.Count - (patchSite + 2);
        chunk.PatchByte(patchSite, (byte)(offset >> 8));
        chunk.PatchByte(patchSite + 1, (byte)(offset & 0xFF));
    }

    /// <summary>
    /// Builds a chunk that calls a zero-arg native which always throws
    /// <see cref="NativeFaultException"/>, then <c>Return</c>s.
    /// </summary>
    private static Chunk BuildFaultingCallChunk(NativeFunction faultingNative) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromFunction(faultingNative));
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)calleeIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Pop, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    private static NativeFunction NewFaultingNative(string leaf, string code, string message) =>
        new("sqrt", 0, (_, _) => throw new NativeFaultException(leaf, code, message));

    // -----------------------------------------------------------------------
    // Unhandled: same top-level-quality diagnostic as a VM-internal fault.
    // -----------------------------------------------------------------------

    [Fact]
    public void Unhandled_NativeFault_ThrowsGrobRuntimeExceptionWithNativeSuppliedCode() {
        var (vm, _) = NewVm();
        Chunk chunk = BuildFaultingCallChunk(
            NewFaultingNative("ArithmeticError", ErrorCatalog.E5006.Code, "sqrt of a negative number"));

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));

        Assert.Equal(ErrorCatalog.E5006.Code, ex.Code);
        Assert.Equal(1, ex.Line);
        Assert.Contains("sqrt of a negative number", ex.Message);
    }

    // -----------------------------------------------------------------------
    // Caught: the routed fault is an ordinary catchable GrobStruct, script resumes.
    // -----------------------------------------------------------------------

    [Fact]
    public void Caught_NativeFault_BindsLeafAndMessage_AndResumes() {
        var script = new Chunk();
        int reachedName = script.AddConstant(GrobValue.FromString("reached"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        var faultingNative = NewFaultingNative("ArithmeticError", ErrorCatalog.E5006.Code, "sqrt of a negative number");
        int calleeIdx = script.AddConstant(GrobValue.FromFunction(faultingNative));
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)calleeIdx, 2);
        script.WriteOpCode(OpCode.Call, 2); script.WriteByte(0, 2);
        script.WriteOpCode(OpCode.Pop, 2);
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
            [new CatchHandler(["ArithmeticError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(0, vm.FrameCount);
        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal("ArithmeticError", s!.TypeName);
        Assert.Contains("sqrt of a negative number", s.GetField("message").AsString());
        Assert.True(vm.Globals["reached"].AsBool());
    }

    // -----------------------------------------------------------------------
    // finally runs exactly once around a native fault, same D-334 partition
    // holds for a native throw as for a user throw or a VM-internal fault.
    // -----------------------------------------------------------------------

    [Fact]
    public void CaughtNativeFault_RunsFinallyExactlyOnce() {
        var script = new Chunk();
        int counterName = script.AddConstant(GrobValue.FromString("ran"));
        int zero0 = script.AddConstant(GrobValue.FromInt(0));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)zero0, 1);
        script.WriteOpCode(OpCode.DefineGlobal, 1); script.WriteByte((byte)counterName, 1);

        int regionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)regionIndex, 2);
        int startOffset = script.Count;

        var faultingNative = NewFaultingNative("ArithmeticError", ErrorCatalog.E5006.Code, "sqrt of a negative number");
        int calleeIdx = script.AddConstant(GrobValue.FromFunction(faultingNative));
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)calleeIdx, 3);
        script.WriteOpCode(OpCode.Call, 3); script.WriteByte(0, 3);
        script.WriteOpCode(OpCode.Pop, 3);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 3);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 3); script.WriteByte(0xFF, 3);

        int handlerOffset = script.Count; // empty catch body — binds at slot 0

        PatchJump16(script, jumpSite);
        int finallyOffset = script.Count;
        // Increment the "ran" global — one instruction, reused directly (no shared
        // helper across files, matching the existing per-file duplication convention).
        script.WriteOpCode(OpCode.GetGlobal, 4); script.WriteByte((byte)counterName, 4);
        int one = script.AddConstant(GrobValue.FromInt(1));
        script.WriteOpCode(OpCode.Constant, 4); script.WriteByte((byte)one, 4);
        script.WriteOpCode(OpCode.AddInt, 4);
        script.WriteOpCode(OpCode.SetGlobal, 4); script.WriteByte((byte)counterName, 4);

        script.WriteOpCode(OpCode.TryEnd, 5);
        script.WriteOpCode(OpCode.Return, 5);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["ArithmeticError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)], finallyOffset));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(1L, vm.Globals["ran"].AsInt());
    }

    [Fact]
    public void NativeFault_NotConstructedAsCSharpExceptionSubtypeOfArithmetic() {
        // The unhandled path uses the base GrobRuntimeException, not a leaf-specific
        // subtype (GrobArithmeticException etc.) — DiagnosticFormatter.WriteRuntime
        // reads Code/Line/Column/Message generically off the base type, and a
        // leaf-name -> C# exception-type mapping table would be unused machinery
        // (no test or formatter distinguishes the subtype for this path).
        var (vm, _) = NewVm();
        Chunk chunk = BuildFaultingCallChunk(
            NewFaultingNative("LookupError", "E1234", "placeholder"));

        var ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));
        Assert.IsType<GrobRuntimeException>(ex);
    }
}
