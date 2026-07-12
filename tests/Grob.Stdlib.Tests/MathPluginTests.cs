using Grob.Core;
using Grob.Vm;
using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment A's proving vertical: <see cref="MathPlugin"/> registers exactly
/// <c>math.pi</c> (a namespace constant) and <c>math.sqrt</c> (a native that throws
/// <c>ArithmeticError</c> on a domain violation) via <see cref="IGrobPlugin"/>, end to
/// end through a real <see cref="VirtualMachine"/>. Chunks are hand-constructed — this
/// project has no dependency on <c>Grob.Compiler</c>.
/// </summary>
public sealed class MathPluginTests {
    private static VirtualMachine NewRegisteredVm() {
        var vm = new VirtualMachine(new StringWriter());
        new MathPlugin().Register(vm);
        return vm;
    }

    private static Chunk BuildGetGlobalChunk(string name) {
        var chunk = new Chunk();
        int idx = chunk.AddConstant(GrobValue.FromString(name));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)idx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    private static Chunk BuildCallChunk(string calleeName, GrobValue arg) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString(calleeName));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)calleeIdx, 1);
        int argIdx = chunk.AddConstant(arg);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)argIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Fact]
    public void MathPi_IsPiToFullDoublePrecision() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetGlobalChunk("math.pi"));

        Assert.Equal(GrobValue.FromFloat(3.141592653589793), vm.Stack.Peek());
    }

    [Fact]
    public void MathSqrt_OfNine_ReturnsThree() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.sqrt", GrobValue.FromFloat(9.0)));

        Assert.Equal(GrobValue.FromFloat(3.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathSqrt_OfZero_ReturnsZero() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.sqrt", GrobValue.FromFloat(0.0)));

        Assert.Equal(GrobValue.FromFloat(0.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathSqrt_OfNegative_ThrowsArithmeticDomainFault() {
        var vm = NewRegisteredVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("math.sqrt", GrobValue.FromFloat(-1.0))));

        Assert.Equal(ErrorCatalog.E5006.Code, ex.Code);
        Assert.Contains("-1", ex.Message);
    }

    [Fact]
    public void MathSqrt_NegativeInsideTryCatch_IsCatchableArithmeticError() {
        // Proves the native-throw seam end to end through the registered plugin:
        // the domain fault unwinds through the SAME handler-table walk a user throw
        // uses, not a bespoke path.
        var script = new Chunk();
        int calleeIdx = script.AddConstant(GrobValue.FromString("math.sqrt"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.GetGlobal, 2); script.WriteByte((byte)calleeIdx, 2);
        int argIdx = script.AddConstant(GrobValue.FromFloat(-4.0));
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)argIdx, 2);
        script.WriteOpCode(OpCode.Call, 2); script.WriteByte(1, 2);
        script.WriteOpCode(OpCode.Pop, 2);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count; // empty catch — binds at slot 0

        int offset = script.Count - (jumpSite + 2);
        script.PatchByte(jumpSite, (byte)(offset >> 8));
        script.PatchByte(jumpSite + 1, (byte)(offset & 0xFF));

        script.WriteOpCode(OpCode.TryEnd, 3);
        script.WriteOpCode(OpCode.Return, 3);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["ArithmeticError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var vm = NewRegisteredVm();
        vm.Run(script);

        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal("ArithmeticError", s!.TypeName);
    }

    [Fact]
    public void Register_AddsExactlyMathPiAndMathSqrt() {
        var vm = new VirtualMachine(new StringWriter());
        new MathPlugin().Register(vm);

        Assert.True(vm.Globals.ContainsKey("math.pi"));
        Assert.True(vm.Globals.ContainsKey("math.sqrt"));
        Assert.Equal(2, vm.Globals.Count);
    }

    [Fact]
    public void Name_IsMath() {
        Assert.Equal("math", new MathPlugin().Name);
    }
}
