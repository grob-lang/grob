using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 5 Increment C: <see cref="NativeFunction"/>
/// transparent dispatch, the re-entrant native↔VM call-back bridge
/// (<see cref="VmInvoker"/>), the four array higher-order methods
/// (<c>filter</c>, <c>select</c>, <c>sort</c>, <c>each</c>), and the
/// <see cref="VirtualMachine.RegisterNative"/> surface.
///
/// All chunks are hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineNativeTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    // -----------------------------------------------------------------------
    // Helpers: chunk builders
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a chunk that:
    /// 1. Pushes the callee value (a constant) onto the stack.
    /// 2. Pushes <paramref name="argValues"/> onto the stack.
    /// 3. Emits <c>Call argCount</c>.
    /// 4. Emits <c>Return</c>.
    /// The result is left on the operand stack for assertions.
    /// </summary>
    private static Chunk BuildCallChunk(GrobValue callee, params GrobValue[] argValues) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(callee);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)calleeIdx, 1);
        foreach (int argIdx in argValues.Select(chunk.AddConstant)) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte((byte)argIdx, 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)argValues.Length, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    /// <summary>
    /// Builds a single-parameter lambda chunk that returns <c>param + delta</c>
    /// (int addition).
    /// </summary>
    private static BytecodeFunction BuildAddDeltaLambda(long delta) {
        var fnChunk = new Chunk();
        int deltaIdx = fnChunk.AddConstant(GrobValue.FromInt(delta));
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)deltaIdx, 1);
        fnChunk.WriteOpCode(OpCode.AddInt, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        // safety-net
        fnChunk.WriteOpCode(OpCode.Nil, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        return new BytecodeFunction("", 1, fnChunk);
    }

    /// <summary>
    /// Builds a single-parameter identity lambda — <c>x =&gt; x</c> — that returns its
    /// argument unchanged.  Used as a key selector for sorting non-int element types
    /// (string/float/bool), where the element itself is the sort key.
    /// </summary>
    private static BytecodeFunction BuildIdentityLambda() {
        var fnChunk = new Chunk();
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        fnChunk.WriteOpCode(OpCode.Nil, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        return new BytecodeFunction("", 1, fnChunk);
    }

    /// <summary>
    /// Builds a single-parameter lambda chunk that returns <c>param &gt; threshold</c>
    /// (int comparison → bool).
    /// </summary>
    private static BytecodeFunction BuildGreaterThanLambda(long threshold) {
        var fnChunk = new Chunk();
        int threshIdx = fnChunk.AddConstant(GrobValue.FromInt(threshold));
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)threshIdx, 1);
        fnChunk.WriteOpCode(OpCode.GreaterInt, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        fnChunk.WriteOpCode(OpCode.Nil, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        return new BytecodeFunction("", 1, fnChunk);
    }

    // -----------------------------------------------------------------------
    // NativeFunction: basic dispatch
    // -----------------------------------------------------------------------

    [Fact]
    public void NativeFunction_Call_DispatchesImplementationAndPushesResult() {
        var (vm, _) = NewVm();
        var native = new NativeFunction("double", 1,
            (args, _) => GrobValue.FromInt(args[0].AsInt() * 2));

        Chunk chunk = BuildCallChunk(
            GrobValue.FromFunction(native),
            GrobValue.FromInt(21));

        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(GrobValue.FromInt(42), vm.Stack.Peek());
    }

    [Fact]
    public void NativeFunction_Call_StackDepthIsOneAfterCall() {
        // The call convention must leave exactly the result on the stack —
        // callee + args are consumed, result pushed.
        var (vm, _) = NewVm();
        var native = new NativeFunction("const42", 0,
            (_, _) => GrobValue.FromInt(42));

        Chunk chunk = BuildCallChunk(GrobValue.FromFunction(native));

        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(GrobValue.FromInt(42), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // Re-entrant bridge: native invokes a lambda argument back through the VM
    // -----------------------------------------------------------------------

    [Fact]
    public void NativeCallBridge_InvokesLambdaAndReturnsResult() {
        // A native that calls its first argument (a lambda) with the value 10.
        // The lambda returns arg + 5, so the result should be 15.
        var (vm, _) = NewVm();
        var native = new NativeFunction("applyTo10", 1, (args, invoker) => {
            GrobValue fn = args[0];
            return invoker(fn, [GrobValue.FromInt(10)]);
        });

        BytecodeFunction lambda = BuildAddDeltaLambda(5); // x => x + 5
        Chunk chunk = BuildCallChunk(
            GrobValue.FromFunction(native),
            GrobValue.FromFunction(lambda));

        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(GrobValue.FromInt(15), vm.Stack.Peek());
    }

    [Fact]
    public void NativeCallBridge_StackDepthIsOneAfterBridgedCall() {
        // After a native → lambda → return chain, the stack holds only the result.
        var (vm, _) = NewVm();
        var native = new NativeFunction("applyToEach", 1, (args, invoker) => {
            GrobValue fn = args[0];
            // Call the lambda three times; return the last result.
            invoker(fn, [GrobValue.FromInt(1)]);
            invoker(fn, [GrobValue.FromInt(2)]);
            return invoker(fn, [GrobValue.FromInt(3)]);
        });

        BytecodeFunction lambda = BuildAddDeltaLambda(10); // x => x + 10
        Chunk chunk = BuildCallChunk(
            GrobValue.FromFunction(native),
            GrobValue.FromFunction(lambda));

        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(GrobValue.FromInt(13), vm.Stack.Peek());
    }

    [Fact]
    public void NativeCallBridge_MultipleInvocations_AllReturnCorrectly() {
        // The native calls the lambda on each of 1..5 and returns their sum.
        var (vm, _) = NewVm();
        var native = new NativeFunction("sumApply", 1, (args, invoker) => {
            GrobValue fn = args[0];
            long sum = 0;
            for (int i = 1; i <= 5; i++)
                sum += invoker(fn, [GrobValue.FromInt(i)]).AsInt();
            return GrobValue.FromInt(sum);
        });

        BytecodeFunction lambda = BuildAddDeltaLambda(0); // x => x (identity)
        Chunk chunk = BuildCallChunk(
            GrobValue.FromFunction(native),
            GrobValue.FromFunction(lambda));

        vm.Run(chunk);

        Assert.Equal(GrobValue.FromInt(15), vm.Stack.Peek()); // 1+2+3+4+5
    }

    // -----------------------------------------------------------------------
    // RegisterNative
    // -----------------------------------------------------------------------

    [Fact]
    public void RegisterNative_AddsGlobalCallable_DispatchesCorrectly() {
        var (vm, _) = NewVm();
        var native = new NativeFunction("greet", 0,
            (_, _) => GrobValue.FromString("hello"));
        vm.RegisterNative("greet", native);

        // GetGlobal "greet", Call 0, Return
        var chunk = new Chunk();
        int nameIdx = chunk.AddConstant(GrobValue.FromString("greet"));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)nameIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        vm.Run(chunk);

        Assert.Equal(GrobValue.FromString("hello"), vm.Stack.Peek());
    }

    [Fact]
    public void RegisterNative_NullName_Throws() {
        var (vm, _) = NewVm();
        var native = new NativeFunction("f", 0, (_, _) => GrobValue.Nil);
        Assert.Throws<ArgumentNullException>(() => vm.RegisterNative(null!, native));
    }

    [Fact]
    public void RegisterNative_NullFunction_Throws() {
        var (vm, _) = NewVm();
        Assert.Throws<ArgumentNullException>(() => vm.RegisterNative("f", null!));
    }

    // -----------------------------------------------------------------------
    // Array higher-order methods (via GetProperty + Call, end-to-end)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a chunk that:
    /// 1. Pushes an array constant.
    /// 2. GetProperty &lt;methodName&gt;.
    /// 3. Pushes the lambda as an argument.
    /// 4. Call 1.
    /// 5. Return.
    /// </summary>
    private static Chunk BuildArrayMethodChunk(
            GrobValue[] elements, string methodName, BytecodeFunction lambda) {
        var chunk = new Chunk();
        // Build array using Constant (array constant)
        var arrayVal = GrobValue.FromArray(new GrobArray(elements));
        int arrIdx = chunk.AddConstant(arrayVal);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)arrIdx, 1);

        // GetProperty <methodName>
        int propIdx = chunk.AddConstant(GrobValue.FromString(methodName));
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte((byte)propIdx, 1);

        // Push lambda arg
        int lambdaIdx = chunk.AddConstant(GrobValue.FromFunction(lambda));
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)lambdaIdx, 1);

        // Call 1
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(1, 1);

        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    /// <summary>
    /// As <see cref="BuildArrayMethodChunk"/>, but supplies a second positional bool
    /// argument (e.g. <c>sort</c>'s <c>descending</c> flag): pushes the array,
    /// GetProperty, the lambda, the bool, then Call 2.
    /// </summary>
    private static Chunk BuildArrayMethodChunkWithBoolArg(
            GrobValue[] elements, string methodName, BytecodeFunction lambda, bool descending) {
        var chunk = new Chunk();
        var arrayVal = GrobValue.FromArray(new GrobArray(elements));
        int arrIdx = chunk.AddConstant(arrayVal);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)arrIdx, 1);

        int propIdx = chunk.AddConstant(GrobValue.FromString(methodName));
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte((byte)propIdx, 1);

        int lambdaIdx = chunk.AddConstant(GrobValue.FromFunction(lambda));
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)lambdaIdx, 1);

        chunk.WriteOpCode(descending ? OpCode.True : OpCode.False, 1);

        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(2, 1);

        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Fact]
    public void Filter_ReturnsSubsetWherePredicateIsTrue() {
        var (vm, _) = NewVm();
        GrobValue[] elements = [
            GrobValue.FromInt(1),
            GrobValue.FromInt(-2),
            GrobValue.FromInt(3),
            GrobValue.FromInt(-4),
        ];
        // lambda: x => x > 0
        BytecodeFunction pred = BuildGreaterThanLambda(0);

        Chunk chunk = BuildArrayMethodChunk(elements, "filter", pred);
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(2, result!.Count);
        Assert.Equal(GrobValue.FromInt(1), result[0]);
        Assert.Equal(GrobValue.FromInt(3), result[1]);
    }

    [Fact]
    public void Filter_EmptyArray_ReturnsEmptyArray() {
        var (vm, _) = NewVm();
        BytecodeFunction pred = BuildGreaterThanLambda(0);

        Chunk chunk = BuildArrayMethodChunk([], "filter", pred);
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(0, result!.Count);
    }

    [Fact]
    public void Select_TransformsEachElement() {
        var (vm, _) = NewVm();
        GrobValue[] elements = [
            GrobValue.FromInt(1),
            GrobValue.FromInt(2),
            GrobValue.FromInt(3),
        ];
        // lambda: x => x + 10
        BytecodeFunction fn = BuildAddDeltaLambda(10);

        Chunk chunk = BuildArrayMethodChunk(elements, "select", fn);
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(3, result!.Count);
        Assert.Equal(GrobValue.FromInt(11), result[0]);
        Assert.Equal(GrobValue.FromInt(12), result[1]);
        Assert.Equal(GrobValue.FromInt(13), result[2]);
    }

    [Fact]
    public void Select_EmptyArray_ReturnsEmptyArray() {
        var (vm, _) = NewVm();
        BytecodeFunction fn = BuildAddDeltaLambda(10);

        Chunk chunk = BuildArrayMethodChunk([], "select", fn);
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(0, result!.Count);
    }

    [Fact]
    public void Sort_ByIntKey_SortsAscendingStably() {
        var (vm, _) = NewVm();
        GrobValue[] elements = [
            GrobValue.FromInt(3),
            GrobValue.FromInt(1),
            GrobValue.FromInt(4),
            GrobValue.FromInt(1),
            GrobValue.FromInt(5),
        ];
        // lambda: x => x (identity key)
        BytecodeFunction keyFn = BuildAddDeltaLambda(0);

        Chunk chunk = BuildArrayMethodChunk(elements, "sort", keyFn);
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(5, result!.Count);
        Assert.Equal(GrobValue.FromInt(1), result[0]);
        Assert.Equal(GrobValue.FromInt(1), result[1]);
        Assert.Equal(GrobValue.FromInt(3), result[2]);
        Assert.Equal(GrobValue.FromInt(4), result[3]);
        Assert.Equal(GrobValue.FromInt(5), result[4]);
    }

    [Fact]
    public void Sort_ByStringKey_SortsOrdinally() {
        var (vm, _) = NewVm();
        GrobValue[] elements = [
            GrobValue.FromString("cherry"),
            GrobValue.FromString("apple"),
            GrobValue.FromString("banana"),
        ];
        Chunk chunk = BuildArrayMethodChunk(elements, "sort", BuildIdentityLambda());
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(GrobValue.FromString("apple"), result![0]);
        Assert.Equal(GrobValue.FromString("banana"), result[1]);
        Assert.Equal(GrobValue.FromString("cherry"), result[2]);
    }

    [Fact]
    public void Sort_ByFloatKey_SortsAscending() {
        var (vm, _) = NewVm();
        GrobValue[] elements = [
            GrobValue.FromFloat(3.5),
            GrobValue.FromFloat(1.2),
            GrobValue.FromFloat(2.8),
        ];
        Chunk chunk = BuildArrayMethodChunk(elements, "sort", BuildIdentityLambda());
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(GrobValue.FromFloat(1.2), result![0]);
        Assert.Equal(GrobValue.FromFloat(2.8), result[1]);
        Assert.Equal(GrobValue.FromFloat(3.5), result[2]);
    }

    [Fact]
    public void Sort_ByBoolKey_OrdersFalseBeforeTrue() {
        var (vm, _) = NewVm();
        GrobValue[] elements = [
            GrobValue.FromBool(true),
            GrobValue.FromBool(false),
            GrobValue.FromBool(true),
            GrobValue.FromBool(false),
        ];
        Chunk chunk = BuildArrayMethodChunk(elements, "sort", BuildIdentityLambda());
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(GrobValue.FromBool(false), result![0]);
        Assert.Equal(GrobValue.FromBool(false), result[1]);
        Assert.Equal(GrobValue.FromBool(true), result[2]);
        Assert.Equal(GrobValue.FromBool(true), result[3]);
    }

    [Fact]
    public void Sort_DescendingFlag_SortsDescending() {
        // The native receives descending=true as a second positional argument.
        var (vm, _) = NewVm();
        GrobValue[] elements = [
            GrobValue.FromInt(1),
            GrobValue.FromInt(3),
            GrobValue.FromInt(2),
        ];
        Chunk chunk = BuildArrayMethodChunkWithBoolArg(
            elements, "sort", BuildIdentityLambda(), descending: true);
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(GrobValue.FromInt(3), result![0]);
        Assert.Equal(GrobValue.FromInt(2), result[1]);
        Assert.Equal(GrobValue.FromInt(1), result[2]);
    }

    [Fact]
    public void Sort_MismatchedKeyTypes_ThrowsRuntimeException() {
        // The key selector returns the element itself, but the elements have mixed
        // kinds (int vs string) — the comparer throws on the first cross-kind compare.
        var (vm, _) = NewVm();
        GrobValue[] elements = [
            GrobValue.FromInt(1),
            GrobValue.FromString("two"),
        ];
        Chunk chunk = BuildArrayMethodChunk(elements, "sort", BuildIdentityLambda());

        Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));
    }

    [Fact]
    public void Sort_NonComparableKeyType_ThrowsRuntimeException() {
        // Sorting by a non-comparable key (nil) of a homogeneous nil array reaches the
        // comparer's unsupported-kind branch.  Comparable validation is deferred to
        // Increment D, so this surfaces as a runtime fault, not a compile error.
        var (vm, _) = NewVm();
        GrobValue[] elements = [GrobValue.Nil, GrobValue.Nil];
        Chunk chunk = BuildArrayMethodChunk(elements, "sort", BuildIdentityLambda());

        Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));
    }

    [Fact]
    public void Each_InvokesLambdaForEveryElementInOrder() {
        var (vm, _) = NewVm();
        var visited = new List<long>();
        var native = new NativeFunction("each", 1, (args, invoker) => {
            // Wrap the array method behaviour for a controlled test:
            // call invoker on elements [10, 20, 30] manually.
            GrobValue fn = args[0];
            foreach (GrobValue arg in new[] { 10L, 20L, 30L }.Select(GrobValue.FromInt)) {
                GrobValue result = invoker(fn, [arg]);
                visited.Add(result.AsInt());
            }
            return GrobValue.Nil;
        });

        // lambda: x => x + 1
        BytecodeFunction lambda = BuildAddDeltaLambda(1);

        Chunk chunk = BuildCallChunk(
            GrobValue.FromFunction(native),
            GrobValue.FromFunction(lambda));

        vm.Run(chunk);

        Assert.Equal([11L, 21L, 31L], visited);
    }

    [Fact]
    public void Each_ViaGetProperty_ReturnsNil() {
        var (vm, _) = NewVm();
        GrobValue[] elements = [
            GrobValue.FromInt(1),
            GrobValue.FromInt(2),
        ];
        // lambda: x => x (identity — side effect not observable without print)
        BytecodeFunction fn = BuildAddDeltaLambda(0);

        Chunk chunk = BuildArrayMethodChunk(elements, "each", fn);
        vm.Run(chunk);

        Assert.Equal(GrobValue.Nil, vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // D-319: cancellation spans the bridge
    // -----------------------------------------------------------------------

    [Fact]
    public void Cancellation_SpansBridge_RunawayLambdaInsideEachIsCancelled() {
        // Proves _steps is a VM-instance field: a runaway lambda invoked by a
        // native is caught by the same token as a top-level runaway loop.
        var (vm, _) = NewVm();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Infinite loop lambda body: while (true) {} — same shape used in
        // VirtualMachineCancellationTests, using corrected loop-offset calculation
        // (offset computed AFTER writing the Loop opcode byte).
        var fnChunk = new Chunk();
        int loopTop = fnChunk.Count;
        fnChunk.WriteOpCode(OpCode.True, 1);
        // JumpIfFalse <exit> — forward jump over loop body
        fnChunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        int exitSite = fnChunk.Count;
        fnChunk.WriteByte(0xFF, 1);
        fnChunk.WriteByte(0xFF, 1);
        // Loop back — offset must be computed AFTER writing the Loop opcode byte
        fnChunk.WriteOpCode(OpCode.Loop, 1);
        int loopOffset = fnChunk.Count + 2 - loopTop; // +2 for the two operand bytes still to write
        fnChunk.WriteByte((byte)(loopOffset >> 8), 1);
        fnChunk.WriteByte((byte)(loopOffset & 0xFF), 1);
        // Patch exit jump (forward, past the loop body)
        int exitOffset = fnChunk.Count - (exitSite + 2);
        fnChunk.PatchByte(exitSite, (byte)(exitOffset >> 8));
        fnChunk.PatchByte(exitSite + 1, (byte)(exitOffset & 0xFF));
        fnChunk.WriteOpCode(OpCode.Nil, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        var runawayLambda = new BytecodeFunction("", 1, fnChunk);

        // Native that calls the lambda on one element — enough to enter the bridge.
        var native = new NativeFunction("once", 1, (args, invoker) => {
            invoker(args[0], [GrobValue.FromInt(0)]);
            return GrobValue.Nil;
        });

        Chunk chunk = BuildCallChunk(
            GrobValue.FromFunction(native),
            GrobValue.FromFunction(runawayLambda));

        Assert.Throws<OperationCanceledException>(() =>
            vm.Run(chunk, cts.Token));
    }
}
