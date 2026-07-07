using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 7 Increment A — the <see cref="OpCode.Throw"/>
/// arm. All chunks are hand-constructed; no compiler dependency (Grob.Vm.Tests
/// never references Grob.Compiler — the DAG boundary holds for tests too).
/// </summary>
public sealed class VirtualMachineThrowTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    // -----------------------------------------------------------------------
    // Top-level throw — frameCount is already 0; hands off via GrobRuntimeException
    // (E5904) carrying the throw site's line, the exception's type name and message.
    // -----------------------------------------------------------------------

    [Fact]
    public void Throw_AtTopLevel_RaisesE5904WithTypeNameAndMessage() {
        var script = new Chunk();
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("IoError", ["message"]));
        int msgConst = script.AddConstant(GrobValue.FromString("file not found: x.txt"));

        script.WriteOpCode(OpCode.Constant, 3); script.WriteByte((byte)msgConst, 3);
        script.WriteOpCode(OpCode.NewStruct, 3); script.WriteByte(typeIdx, 3);
        script.WriteOpCode(OpCode.Throw, 3);
        script.WriteOpCode(OpCode.Return, 3);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(3, ex.Line);
        Assert.Contains("IoError", ex.Message);
        Assert.Contains("file not found: x.txt", ex.Message);
        Assert.Equal(0, vm.FrameCount);
    }

    // -----------------------------------------------------------------------
    // Throw from inside a called function — unwinds every frame back to the
    // top and closes upvalues opened before the throw (D-325), with no
    // value-stack underflow.
    // -----------------------------------------------------------------------

    [Fact]
    public void Throw_InsideCalledFunction_UnwindsFramesAndClosesOpenUpvalue() {
        // Lambda: reads its captured upvalue (x) and returns it.
        var lambdaChunk = new Chunk();
        lambdaChunk.WriteOpCode(OpCode.GetUpvalue, 1); lambdaChunk.WriteByte(0, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        lambdaChunk.WriteOpCode(OpCode.Nil, 1);
        lambdaChunk.WriteOpCode(OpCode.Return, 1);
        var lambdaFn = new BytecodeFunction(string.Empty, 0, lambdaChunk, upvalueCount: 1);

        // throwerFn: x := 999 (slot 0); capture x in a closure and stash it in a
        // global (so it survives the frame this test is about to tear down);
        // then construct an IoError and throw it.
        var throwerChunk = new Chunk();
        int nineNineNine = throwerChunk.AddConstant(GrobValue.FromInt(999));
        int lambdaIdx = throwerChunk.AddConstant(GrobValue.FromFunction(lambdaFn));
        int globalName = throwerChunk.AddConstant(GrobValue.FromString("capturedClosure"));
        int msgConst = throwerChunk.AddConstant(GrobValue.FromString("boom"));
        byte typeIdx = throwerChunk.AddStructType(new StructTypeDescriptor("IoError", ["message"]));

        throwerChunk.WriteOpCode(OpCode.Constant, 1); throwerChunk.WriteByte((byte)nineNineNine, 1); // x := 999 (slot 0)
        throwerChunk.WriteOpCode(OpCode.Closure, 1); throwerChunk.WriteByte((byte)lambdaIdx, 1);
        throwerChunk.WriteByte(1, 1); throwerChunk.WriteByte(0, 1);                                    // isLocal=1 slot=0
        throwerChunk.WriteOpCode(OpCode.DefineGlobal, 1); throwerChunk.WriteByte((byte)globalName, 1); // stash closure globally

        throwerChunk.WriteOpCode(OpCode.Constant, 2); throwerChunk.WriteByte((byte)msgConst, 2);
        throwerChunk.WriteOpCode(OpCode.NewStruct, 2); throwerChunk.WriteByte(typeIdx, 2);
        throwerChunk.WriteOpCode(OpCode.Throw, 2);
        throwerChunk.WriteOpCode(OpCode.Nil, 2);   // unreachable safety-net
        throwerChunk.WriteOpCode(OpCode.Return, 2);
        var throwerFn = new BytecodeFunction("thrower", 0, throwerChunk);

        var script = new Chunk();
        int fnIdx = script.AddConstant(GrobValue.FromFunction(throwerFn));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)fnIdx, 1);
        script.WriteOpCode(OpCode.Call, 1); script.WriteByte(0, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5904.Code, ex.Code);
        Assert.Equal(2, ex.Line);
        // Every frame was popped back to the top — no dangling call depth.
        Assert.Equal(0, vm.FrameCount);

        // The upvalue captured before the throw was closed to the heap correctly
        // (not left dangling on a torn-down/reused stack slot): a fresh Run on the
        // same VM instance retrieves the stashed closure by its (persistent)
        // global name and calls it, reading the closed value back correctly.
        var readBack = new Chunk();
        int nameConst = readBack.AddConstant(GrobValue.FromString("capturedClosure"));
        readBack.WriteOpCode(OpCode.GetGlobal, 1); readBack.WriteByte((byte)nameConst, 1);
        readBack.WriteOpCode(OpCode.Call, 1); readBack.WriteByte(0, 1);
        readBack.WriteOpCode(OpCode.Return, 1);

        vm.Run(readBack);
        Assert.Equal(999L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Layer-invariant — pathological but parseable struct shapes never crash the
    // VM with a host exception; the opcode only ever raises the sanctioned
    // GrobRuntimeException.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Throw_PathologicalStructShapes_NeverThrowsHostException(bool hasMessageField) {
        var script = new Chunk();
        string[] fields = hasMessageField ? ["message"] : [];
        byte typeIdx = script.AddStructType(new StructTypeDescriptor("RuntimeError", fields));

        if (hasMessageField) {
            int msgConst = script.AddConstant(GrobValue.FromString("x"));
            script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)msgConst, 1);
        }
        script.WriteOpCode(OpCode.NewStruct, 1); script.WriteByte(typeIdx, 1);
        script.WriteOpCode(OpCode.Throw, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        // A controlled GrobRuntimeException is fine; anything else (a bare CLR
        // exception escaping the sanctioned hierarchy) is the failure this guards.
        Exception? ex = Record.Exception(() => vm.Run(script));
        Assert.IsType<GrobRuntimeException>(ex);
    }

    [Fact]
    public void Throw_OnNonStructValue_RaisesControlledInternalException() {
        // A non-struct operand can only reach Throw via a hand-built chunk or a
        // type-checker defect — the real pipeline always proves a GrobError
        // subtype first. Defence in depth: GrobInternalException, not a bare
        // CLR crash (InvalidCastException, NullReferenceException, etc).
        var script = new Chunk();
        int fortyTwo = script.AddConstant(GrobValue.FromInt(42));
        script.WriteOpCode(OpCode.Constant, 1); script.WriteByte((byte)fortyTwo, 1);
        script.WriteOpCode(OpCode.Throw, 1);
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        Assert.Throws<GrobInternalException>(() => vm.Run(script));
    }
}
