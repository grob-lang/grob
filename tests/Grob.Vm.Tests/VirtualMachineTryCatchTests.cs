using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 7 Increment B — the <see cref="OpCode.Throw"/>
/// arm's unwind-to-handler search. All chunks are hand-constructed; no compiler
/// dependency (Grob.Vm.Tests never references Grob.Compiler — the DAG boundary
/// holds for tests too).
/// </summary>
public sealed class VirtualMachineTryCatchTests {
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

    // -----------------------------------------------------------------------
    // A typed catch matches a matching throw and binds the value.
    // -----------------------------------------------------------------------

    [Fact]
    public void TypedCatch_MatchingThrow_BindsExceptionIntoSlot() {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)msgConst, 2);
        script.WriteOpCode(OpCode.NewStruct, 2); script.WriteByte(typeIdx, 2);
        script.WriteOpCode(OpCode.Throw, 2);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count; // empty catch body

        PatchJump16(script, jumpSite);
        script.WriteOpCode(OpCode.TryEnd, 3);
        script.WriteOpCode(OpCode.Return, 3);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["IoError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(0, vm.FrameCount);
        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal("IoError", s!.TypeName);
        Assert.Equal("boom", s.GetField("message").AsString());
    }

    // -----------------------------------------------------------------------
    // A non-matching typed catch does not catch — the throw propagates past it.
    // -----------------------------------------------------------------------

    [Fact]
    public void TypedCatch_NonMatchingThrow_PropagatesToTopLevel() {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)msgConst, 2);
        script.WriteOpCode(OpCode.NewStruct, 2); script.WriteByte(typeIdx, 2);
        script.WriteOpCode(OpCode.Throw, 2, 5);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count;

        PatchJump16(script, jumpSite);
        script.WriteOpCode(OpCode.TryEnd, 3);
        script.WriteOpCode(OpCode.Return, 3);

        // Catch only NetworkError — the thrown IoError does not match.
        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["NetworkError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Contains("IoError", ex.Message);
        // The throw site was seeded at (2, 5); the unwind/report path must carry
        // those exact coordinates through to the top-level diagnostic.
        Assert.Equal(2, ex.Line);
        Assert.Equal(5, ex.Column);
        Assert.Equal(0, vm.FrameCount);
    }

    // -----------------------------------------------------------------------
    // The catch-all catches anything not matched by an earlier typed handler.
    // -----------------------------------------------------------------------

    [Fact]
    public void CatchAll_CatchesWhatEarlierTypedHandlerMisses() {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)msgConst, 2);
        script.WriteOpCode(OpCode.NewStruct, 2); script.WriteByte(typeIdx, 2);
        script.WriteOpCode(OpCode.Throw, 2);
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
        Assert.Equal("IoError", s!.TypeName);
    }

    // -----------------------------------------------------------------------
    // Source order decides first match: an earlier broad handler wins over a
    // later, more specific one that would also match.
    // -----------------------------------------------------------------------

    [Fact]
    public void SourceOrder_EarlierMatchingHandlerWinsOverLaterOne() {
        var script = new Chunk();
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)msgConst, 2);
        script.WriteOpCode(OpCode.NewStruct, 2); script.WriteByte(typeIdx, 2);
        script.WriteOpCode(OpCode.Throw, 2);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        // First handler: GrobError root — resolved to every leaf name, so it
        // matches an IoError throw too. Marks the binding slot with 100.
        int firstHandlerOffset = script.Count;
        int hundred = script.AddConstant(GrobValue.FromInt(100));
        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)hundred, 3);
        script.WriteOpCode(OpCode.SetLocal, 3); script.WriteByte(0, 3); // SetLocal consumes the pushed value
        script.WriteOpCode(OpCode.Jump, 3); // skip the second handler
        int skipSecondSite = script.Count;
        script.WriteByte(0xFF, 3); script.WriteByte(0xFF, 3);

        // Second handler: IoError exactly — would also match, but never runs
        // because the first (broader) handler is tried first.
        int secondHandlerOffset = script.Count;
        int twoHundred = script.AddConstant(GrobValue.FromInt(200));
        script.WriteOpCode(OpCode.Constant, 4); script.WriteByte((byte)twoHundred, 4);
        script.WriteOpCode(OpCode.SetLocal, 4); script.WriteByte(0, 4);

        PatchJump16(script, skipSecondSite);
        PatchJump16(script, jumpSite);
        script.WriteOpCode(OpCode.TryEnd, 5);
        script.WriteOpCode(OpCode.Return, 5);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset, [
            new CatchHandler(["GrobError", "IoError", "NetworkError", "JsonError", "ProcessError",
                "NilError", "ArithmeticError", "IndexError", "ParseError", "LookupError", "RuntimeError"],
                IsCatchAll: false, firstHandlerOffset, BindingSlot: 0),
            new CatchHandler(["IoError"], IsCatchAll: false, secondHandlerOffset, BindingSlot: 0),
        ]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(100L, vm.Stack.GetSlot(0).AsInt());
    }

    // -----------------------------------------------------------------------
    // A throw from a nested call frame inside a try unwinds the frames to the
    // handler, closes upvalues by location (D-325), and leaves an exact value
    // stack with no underflow.
    // -----------------------------------------------------------------------

    [Fact]
    public void Throw_FromNestedCallFrame_UnwindsToHandlerAndClosesUpvalue() {
        // Lambda: reads its captured upvalue (x) and returns it.
        var lambdaChunk = new Chunk();
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        lambdaChunk.WriteOpCode(OpCode.Nil, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        // thrower(): x := 999 (slot 0); capture x in a closure and stash it in a
        // global; then construct an IoError and throw it. Never returns normally.
        var throwerChunk = new Chunk();
        int nineNineNine = throwerChunk.AddConstant(GrobValue.FromInt(999));
        int lambdaIdx = throwerChunk.AddConstant(GrobValue.FromFunction(lambdaFn));
        int globalName = throwerChunk.AddConstant(GrobValue.FromString("capturedClosure"));
        int msgConst = throwerChunk.AddConstant(GrobValue.FromString("boom"));
        byte typeIdx = throwerChunk.AddStructType(new StructTypeDescriptor("IoError", ["message"]));

        throwerChunk.WriteOpCode(OpCode.Constant, 1); throwerChunk.WriteByte((byte)nineNineNine, 1);
        throwerChunk.WriteOpCode(OpCode.Closure, 1); throwerChunk.WriteByte((byte)lambdaIdx, 1);
        throwerChunk.WriteByte(1, 1); throwerChunk.WriteByte(0, 1);
        throwerChunk.WriteOpCode(OpCode.DefineGlobal, 1); throwerChunk.WriteByte((byte)globalName, 1);

        throwerChunk.WriteOpCode(OpCode.Constant, 2); throwerChunk.WriteByte((byte)msgConst, 2);
        throwerChunk.WriteOpCode(OpCode.NewStruct, 2); throwerChunk.WriteByte(typeIdx, 2);
        throwerChunk.WriteOpCode(OpCode.Throw, 2);
        throwerChunk.WriteOpCode(OpCode.Nil, 2);
        throwerChunk.WriteOpCode(OpCode.Return, 2);
        var throwerFn = new BytecodeFunction("thrower", 0, throwerChunk);

        var script = new Chunk();
        int fnIdx = script.AddConstant(GrobValue.FromFunction(throwerFn));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)fnIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Pop, 1); // discard the (never-reached) return value
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 1);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 1); script.WriteByte(0xFF, 1);

        int handlerOffset = script.Count; // empty catch body — binds at slot 0

        PatchJump16(script, jumpSite);
        script.WriteOpCode(OpCode.TryEnd, 3);
        script.WriteOpCode(OpCode.Return, 3);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["IoError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var (vm, _) = NewVm();
        vm.Run(script);

        // The exception was caught, not propagated — every frame is unwound back
        // to the top, and the value stack holds exactly the bound exception, no
        // leaked call-frame operands from the abandoned Call.
        Assert.Equal(0, vm.FrameCount);
        Assert.Equal(1, vm.Stack.Count);
        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal("IoError", s!.TypeName);

        // The upvalue captured before the throw was closed to the heap correctly:
        // a fresh Run retrieves the stashed closure by its (persistent) global
        // name and calls it, reading the closed value back correctly.
        var readBack = new Chunk();
        int nameConst = readBack.AddConstant(GrobValue.FromString("capturedClosure"));
        readBack.WriteOpCode(OpCode.GetGlobal, 1); readBack.WriteByte((byte)nameConst, 1);
        readBack.WriteOpCode(OpCode.Call, 1); readBack.WriteByte(0, 1);
        readBack.WriteOpCode(OpCode.Return, 1);

        vm.Run(readBack);
        Assert.Equal(999L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Per-region independence: a throw in an inner try matched there does not
    // disturb an outer try's region — the outer catch body never runs.
    // -----------------------------------------------------------------------

    [Fact]
    public void NestedTry_InnerMatch_DoesNotDisturbOuterRegion() {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int innerGlobal = script.AddConstant(GrobValue.FromString("innerCaught"));
        int outerGlobal = script.AddConstant(GrobValue.FromString("outerCaught"));

        int outerRegionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)outerRegionIndex, 1);
        int outerStart = script.Count;

        int innerRegionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)innerRegionIndex, 2);
        int innerStart = script.Count;

        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)msgConst, 3);
        script.WriteOpCode(OpCode.NewStruct, 3); script.WriteByte(typeIdx, 3);
        script.WriteOpCode(OpCode.Throw, 3);
        int innerEnd = script.Count;

        script.WriteOpCode(OpCode.Jump, 3);
        int innerJumpSite = script.Count;
        script.WriteByte(0xFF, 3); script.WriteByte(0xFF, 3);

        int innerHandlerOffset = script.Count;
        script.WriteOpCode(OpCode.True, 4);
        script.WriteOpCode(OpCode.DefineGlobal, 4); script.WriteByte((byte)innerGlobal, 4);

        PatchJump16(script, innerJumpSite);
        script.WriteOpCode(OpCode.TryEnd, 4);
        int outerEnd = script.Count;

        script.WriteOpCode(OpCode.Jump, 5);
        int outerJumpSite = script.Count;
        script.WriteByte(0xFF, 5); script.WriteByte(0xFF, 5);

        int outerHandlerOffset = script.Count;
        script.WriteOpCode(OpCode.True, 6);
        script.WriteOpCode(OpCode.DefineGlobal, 6); script.WriteByte((byte)outerGlobal, 6);

        PatchJump16(script, outerJumpSite);
        script.WriteOpCode(OpCode.TryEnd, 6);
        script.WriteOpCode(OpCode.Return, 6);

        script.SetTryRegion(innerRegionIndex, new TryRegion(innerStart, innerEnd,
            [new CatchHandler(["IoError"], IsCatchAll: false, innerHandlerOffset, BindingSlot: 0)]));
        script.SetTryRegion(outerRegionIndex, new TryRegion(outerStart, outerEnd,
            [new CatchHandler(["IoError"], IsCatchAll: false, outerHandlerOffset, BindingSlot: 0)]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.True(vm.Globals.ContainsKey("innerCaught"));
        Assert.False(vm.Globals.ContainsKey("outerCaught"));
    }

    // -----------------------------------------------------------------------
    // When the innermost containing region has no matching handler, the search
    // falls through to the next-less-nested containing region in the same
    // chunk — before ever considering a frame pop.
    // -----------------------------------------------------------------------

    [Fact]
    public void NestedTry_InnerNoMatch_FallsThroughToOuterRegionInSameChunk() {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));
        int outerGlobal = script.AddConstant(GrobValue.FromString("outerCaught"));

        int outerRegionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)outerRegionIndex, 1);
        int outerStart = script.Count;

        int innerRegionIndex = script.AddTryRegion();
        script.WriteOpCode(OpCode.TryBegin, 2); script.WriteByte((byte)innerRegionIndex, 2);
        int innerStart = script.Count;

        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)msgConst, 3);
        script.WriteOpCode(OpCode.NewStruct, 3); script.WriteByte(typeIdx, 3);
        script.WriteOpCode(OpCode.Throw, 3);
        int innerEnd = script.Count;

        script.WriteOpCode(OpCode.Jump, 3);
        int innerJumpSite = script.Count;
        script.WriteByte(0xFF, 3); script.WriteByte(0xFF, 3);

        int innerHandlerOffset = script.Count; // unreachable — the inner handler never matches
        script.WriteOpCode(OpCode.Nil, 4);
        script.WriteOpCode(OpCode.Pop, 4);

        PatchJump16(script, innerJumpSite);
        script.WriteOpCode(OpCode.TryEnd, 4);
        int outerEnd = script.Count;

        script.WriteOpCode(OpCode.Jump, 5);
        int outerJumpSite = script.Count;
        script.WriteByte(0xFF, 5); script.WriteByte(0xFF, 5);

        int outerHandlerOffset = script.Count;
        script.WriteOpCode(OpCode.True, 6);
        script.WriteOpCode(OpCode.DefineGlobal, 6); script.WriteByte((byte)outerGlobal, 6);

        PatchJump16(script, outerJumpSite);
        script.WriteOpCode(OpCode.TryEnd, 6);
        script.WriteOpCode(OpCode.Return, 6);

        // The inner region only catches NetworkError — an IoError throw does not
        // match, so the search must walk out to the outer region in this same
        // chunk (not pop any frame — there is only the top-level script here).
        script.SetTryRegion(innerRegionIndex, new TryRegion(innerStart, innerEnd,
            [new CatchHandler(["NetworkError"], IsCatchAll: false, innerHandlerOffset, BindingSlot: 0)]));
        script.SetTryRegion(outerRegionIndex, new TryRegion(outerStart, outerEnd,
            [new CatchHandler(["IoError"], IsCatchAll: false, outerHandlerOffset, BindingSlot: 0)]));

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.True(vm.Globals.ContainsKey("outerCaught"));
        Assert.Equal(0, vm.FrameCount);
    }

    // -----------------------------------------------------------------------
    // Layer-invariant — pathological but well-formed handler tables never
    // crash the VM with a host exception; only the sanctioned GrobRuntimeException.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(true)]  // a region exists but has zero handlers — falls through untouched
    [InlineData(false)] // no try region at all
    public void Throw_PathologicalHandlerTableShapes_NeverThrowsHostException(bool emptyRegionPresent) {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("boom"));

        int? regionIndex = emptyRegionPresent ? script.AddTryRegion() : null;
        int startOffset = 0;
        if (regionIndex is not null) {
            script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
            startOffset = script.Count;
        }

        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)msgConst, 1);
        script.WriteOpCode(OpCode.NewStruct, 1); script.WriteByte(typeIdx, 1);
        script.WriteOpCode(OpCode.Throw, 1);
        script.WriteOpCode(OpCode.Return, 1);

        if (regionIndex is not null) {
            script.SetTryRegion(regionIndex.Value, new TryRegion(startOffset, script.Count, []));
        }

        var (vm, _) = NewVm();
        Exception? ex = Record.Exception(() => vm.Run(script));
        Assert.IsType<GrobRuntimeException>(ex);
    }
}
