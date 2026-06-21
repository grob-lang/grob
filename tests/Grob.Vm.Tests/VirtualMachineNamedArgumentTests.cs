using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 5 Increment B — confirming the compiler's
/// reorder-and-fill emission produces a positional argument list the (unchanged)
/// <see cref="OpCode.Call"/> handler executes correctly.
/// </summary>
/// <remarks>
/// The VM is unchanged by Increment B: it sees a fully-bound positional argument
/// list exactly as in Increment A. This test hand-builds the chunk shape the
/// compiler emits for a call that mixes a positional argument, a named argument and
/// an omitted default — the materialised list in parameter declaration order — and
/// asserts the callee computes the expected result.
/// </remarks>
public sealed class VirtualMachineNamedArgumentTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    [Fact]
    public void Call_ReorderedAndFilledArguments_ReturnsCorrectValue() {
        // Models: fn add(a: int, b: int = 99, c: int = 0): int { return a + b + c }
        // called as add(1, c: 5) — the compiler materialises the positional list
        // [a=1, b=99 (default), c=5 (named)] in parameter declaration order.
        var fnChunk = new Chunk();
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1); // slot 0 = a
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(1, 1); // slot 1 = b
        fnChunk.WriteOpCode(OpCode.AddInt, 1);
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(2, 1); // slot 2 = c
        fnChunk.WriteOpCode(OpCode.AddInt, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        var fn = new BytecodeFunction("add", 3, fnChunk);

        var script = new Chunk();
        int fnConst = script.AddConstant(GrobValue.FromFunction(fn));
        int one = script.AddConstant(GrobValue.FromInt(1));
        int ninetyNine = script.AddConstant(GrobValue.FromInt(99));
        int five = script.AddConstant(GrobValue.FromInt(5));

        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)fnConst, 1);
        // Arguments in parameter declaration order: a=1, b=99 (default), c=5 (named).
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)one, 1);
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)ninetyNine, 1);
        script.WriteOpCode(OpCode.Constant, 1);
        script.WriteByte((byte)five, 1);
        script.WriteOpCode(OpCode.Call, 1);
        script.WriteByte(3, 1); // arg count = parameter count
        script.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(script);

        Assert.Equal(105L, vm.Stack.Peek().AsInt()); // 1 + 99 + 5
    }
}
