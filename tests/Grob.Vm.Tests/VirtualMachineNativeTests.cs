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

    /// <summary>
    /// Builds a single-parameter lambda chunk that returns <c>param * 2</c> (int
    /// multiplication) — used as the innermost, real (non-native) callee in the
    /// cross-native nesting test, so that test exercises both nested-native
    /// dispatch (D-397) AND a nested BytecodeFunction dispatch in the same chain.
    /// </summary>
    private static BytecodeFunction BuildDoubleLambda() {
        var fnChunk = new Chunk();
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);
        fnChunk.WriteOpCode(OpCode.MultiplyInt, 1);
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
            return invoker.Invoke(fn, [GrobValue.FromInt(10)]);
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
            invoker.Invoke(fn, [GrobValue.FromInt(1)]);
            invoker.Invoke(fn, [GrobValue.FromInt(2)]);
            return invoker.Invoke(fn, [GrobValue.FromInt(3)]);
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
                sum += invoker.Invoke(fn, [GrobValue.FromInt(i)]).AsInt();
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
    /// <paramref name="lambda"/> takes <see cref="GrobFunction"/> rather than
    /// <see cref="BytecodeFunction"/> so a caller can also supply a bound
    /// <see cref="NativeFunction"/> (D-397 cross-native nesting test).
    /// </summary>
    private static Chunk BuildArrayMethodChunk(
            GrobValue[] elements, string methodName, GrobFunction lambda) {
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
                GrobValue result = invoker.Invoke(fn, [arg]);
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
    // D-394: GetProperty's array arm builds no ct/finallyContext of its own any
    // more (ArrayNatives.GetMethod's bind-time VmInvoker parameter was dead —
    // every higher-order member takes its VmInvoker from its own
    // NativeFunction.Implementation delegate at invocation time, from the Call
    // handler or from InvokeCallable when it runs re-entrantly). This covers the
    // behavioural half of that deletion: a lambda argument that faults inside a
    // bound higher-order call is still caught and resumes correctly on the
    // invocation-time FinallyContext alone. That no bind-time context is built is
    // established by source inspection and the D-394 allocation measurement, not
    // by this test.
    // -----------------------------------------------------------------------

    private static void PatchJump16(Chunk chunk, int patchSite) {
        int offset = chunk.Count - (patchSite + 2);
        chunk.PatchByte(patchSite, (byte)(offset >> 8));
        chunk.PatchByte(patchSite + 1, (byte)(offset & 0xFF));
    }

    [Fact]
    public void Filter_LambdaFaultCaughtAndResumes() {
        var script = new Chunk();
        int reachedName = script.AddConstant(GrobValue.FromString("reached"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        GrobValue arrayVal = GrobValue.FromArray(new GrobArray([GrobValue.FromInt(1)]));
        int arrIdx = script.AddConstant(arrayVal);
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)arrIdx, 2);

        int propIdx = script.AddConstant(GrobValue.FromString("filter"));
        script.WriteOpCode(OpCode.GetProperty, 2); script.WriteByte((byte)propIdx, 2);

        var faultingPredicate = new NativeFunction("faultingPredicate", 1,
            (_, _) => throw new NativeFaultException(
                "ArithmeticError", ErrorCatalog.E5006.Code, "predicate faulted"));
        int lambdaIdx = script.AddConstant(GrobValue.FromFunction(faultingPredicate));
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)lambdaIdx, 2);

        script.WriteOpCode(OpCode.Call, 2); script.WriteByte(1, 2);
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
        Assert.Contains("predicate faulted", s.GetField("message").AsString());
        Assert.True(vm.Globals["reached"].AsBool());
    }

    /// <summary>
    /// Sibling to <see cref="Filter_LambdaFaultCaughtAndResumes"/>: this variant has no
    /// enclosing <c>try</c>/<c>catch</c>, so the fault raised inside the lambda
    /// <c>filter</c> invokes through the nested-native bridge escapes as an unhandled
    /// <see cref="GrobRuntimeException"/>. D-397 asserts its numeric
    /// <see cref="GrobRuntimeException.Line"/>/<see cref="GrobRuntimeException.Column"/>
    /// match the ORIGINAL <c>Call</c> opcode's source position — not some
    /// bridge-internal value the <c>VmInvoker</c> struct conversion could silently
    /// substitute. <see cref="Filter_LambdaFaultCaughtAndResumes"/> only ever checked
    /// the leaf type name and message; this test is the numeric-location half.
    /// </summary>
    [Fact]
    public void Filter_LambdaFault_Uncaught_ReportsOriginalCallSiteLineAndColumn() {
        var script = new Chunk();

        GrobValue arrayVal = GrobValue.FromArray(new GrobArray([GrobValue.FromInt(1)]));
        int arrIdx = script.AddConstant(arrayVal);
        script.WriteOpCode(OpCode.Constant, 5, 3); script.WriteByte((byte)arrIdx, 5, 3);

        int propIdx = script.AddConstant(GrobValue.FromString("filter"));
        script.WriteOpCode(OpCode.GetProperty, 5, 3); script.WriteByte((byte)propIdx, 5, 3);

        var faultingPredicate = new NativeFunction("faultingPredicate", 1,
            (_, _) => throw new NativeFaultException(
                "ArithmeticError", ErrorCatalog.E5006.Code, "predicate faulted"));
        int lambdaIdx = script.AddConstant(GrobValue.FromFunction(faultingPredicate));
        script.WriteOpCode(OpCode.Constant, 5, 3); script.WriteByte((byte)lambdaIdx, 5, 3);

        // The Call opcode itself is the source location that must survive the bridge.
        script.WriteOpCode(OpCode.Call, 5, 12); script.WriteByte(1, 5, 12);
        script.WriteOpCode(OpCode.Pop, 5, 12);
        script.WriteOpCode(OpCode.Nil, 6, 1);
        script.WriteOpCode(OpCode.Return, 6, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(script));

        Assert.Equal(ErrorCatalog.E5006.Code, ex.Code);
        Assert.Equal(5, ex.Line);
        Assert.Equal(12, ex.Column);
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
            invoker.Invoke(args[0], [GrobValue.FromInt(0)]);
            return GrobValue.Nil;
        });

        Chunk chunk = BuildCallChunk(
            GrobValue.FromFunction(native),
            GrobValue.FromFunction(runawayLambda));

        Assert.Throws<OperationCanceledException>(() =>
            vm.Run(chunk, cts.Token));
    }

    // -----------------------------------------------------------------------
    // Cross-native nested invocation (D-397): a lambda invoked by ONE
    // higher-order native itself invokes a DIFFERENT higher-order native, on a
    // different receiver — the previously-untested branch of InvokeCallable's
    // nested-native path. Existing coverage only exercises same-native
    // self-recursion (Each_InvokesLambdaForEveryElementInOrder-style single-level
    // bridging) and the FinallyContext-propagation path
    // (Filter_LambdaFaultCaughtAndResumes); neither nests a SECOND native inside
    // the first's lambda argument.
    // -----------------------------------------------------------------------

    [Fact]
    public void CrossNativeNestedInvocation_FilterLambdaInvokesSelectOnDifferentArray_CompletesCorrectly() {
        // Three levels deep, each through the re-entrant bridge:
        //   outer.filter(crossPredicate)                     -- level 1, Call handler's invoker
        //     crossPredicate invokes inner.select(doubleLambda) -- level 2, InvokeCallable's
        //                                                          nested-native path (native -> native)
        //       select invokes doubleLambda for each inner element -- level 3, InvokeCallable's
        //                                                              BytecodeFunction path (native -> real frame)
        var (vm, _) = NewVm();

        var innerArr = new GrobArray([GrobValue.FromInt(1), GrobValue.FromInt(2), GrobValue.FromInt(3)]);
        BytecodeFunction doubleLambda = BuildDoubleLambda(); // x => x * 2
        NativeFunction selectOnInner = ArrayNatives.GetMethod("select", innerArr)!;

        // crossPredicate(e): doubles innerArr via a DIFFERENT higher-order native
        // (select, not filter) invoked from inside filter's own lambda argument, then
        // compares e against the doubled sum (innerArr doubled = [2,4,6], sum = 12).
        var crossPredicate = new NativeFunction("crossPredicate", 1, (args, invoker) => {
            GrobValue selected = invoker.Invoke(GrobValue.FromFunction(selectOnInner),
                [GrobValue.FromFunction(doubleLambda)]);
            Assert.True(selected.TryAsArray(out GrobArray? selectedArr));
            long sum = 0;
            for (int i = 0; i < selectedArr!.Count; i++) sum += selectedArr[i].AsInt();
            return GrobValue.FromBool(args[0].AsInt() > sum);
        });

        GrobValue[] outerElements = [GrobValue.FromInt(100), GrobValue.FromInt(1)];
        Chunk chunk = BuildArrayMethodChunk(outerElements, "filter", crossPredicate);

        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(1, result!.Count);
        Assert.Equal(GrobValue.FromInt(100), result[0]);
    }
}
