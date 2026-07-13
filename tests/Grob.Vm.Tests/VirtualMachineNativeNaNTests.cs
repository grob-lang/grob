using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// Sprint 8 Increment B is the first point a native can hand the VM a <c>NaN</c> or
/// <c>±Infinity</c> <see cref="GrobValue"/> (<c>math.pow(-2.0, 0.5)</c> etc. — see the
/// comment on <c>VirtualMachineConditionalTests.Equal_NaNvsNaN_ReturnsFalse</c>, which
/// flagged this as not yet reachable). These functions follow plain IEEE 754 semantics
/// and never throw (D-342's math section), so this covers the VM-level invariant: a
/// non-finite value returned from a native call is an ordinary <see cref="GrobValue"/>
/// that flows through a subsequent arithmetic opcode without faulting the VM. All chunks
/// are hand-constructed — no compiler or stdlib dependency.
/// </summary>
public sealed class VirtualMachineNativeNaNTests {
    private static NativeFunction NewReturningNative(string name, GrobValue result) =>
        new(name, 0, (_, _) => result);

    /// <summary>Builds: call the zero-arg native, then Add its result to <paramref name="addend"/>.</summary>
    private static Chunk BuildCallThenAddChunk(NativeFunction native, double addend) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromFunction(native));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)calleeIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(0, 1);

        int addendIdx = chunk.AddConstant(GrobValue.FromFloat(addend));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)addendIdx, 1);
        chunk.WriteOpCode(OpCode.AddFloat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NativeReturningNonFiniteFloat_DoesNotThrow(double value) {
        var vm = new VirtualMachine(new StringWriter());
        var native = NewReturningNative("nonFinite", GrobValue.FromFloat(value));

        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromFunction(native));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)calleeIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var ex = Record.Exception(() => vm.Run(chunk));

        Assert.Null(ex);
        Assert.Equal(value, vm.Stack.Peek().AsFloat());
    }

    [Fact]
    public void NaNFromNative_PropagatesSilentlyThroughSubsequentAddFloat() {
        var vm = new VirtualMachine(new StringWriter());
        var native = NewReturningNative("nan", GrobValue.FromFloat(double.NaN));

        vm.Run(BuildCallThenAddChunk(native, 1.0));

        Assert.True(double.IsNaN(vm.Stack.Peek().AsFloat()));
    }

    [Fact]
    public void PositiveInfinityFromNative_PropagatesThroughSubsequentAddFloat() {
        var vm = new VirtualMachine(new StringWriter());
        var native = NewReturningNative("inf", GrobValue.FromFloat(double.PositiveInfinity));

        vm.Run(BuildCallThenAddChunk(native, 1.0));

        Assert.Equal(double.PositiveInfinity, vm.Stack.Peek().AsFloat());
    }
}
