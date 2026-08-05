using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// D-393 Q2 (D-398) — the per-receiver <see cref="NativeFunction"/> cache on
/// <see cref="GrobArray"/>/<see cref="GrobMap"/>: <see cref="ArrayNatives.GetMethod"/>
/// and <see cref="MapNatives.GetMethod"/> return the SAME bound instance for a repeat
/// bind against the same receiver and method name, rather than constructing a fresh
/// one on every <see cref="OpCode.GetProperty"/> dispatch. Deliberately exercises
/// <c>contains</c>/<c>filter</c>/<c>get</c> rather than <c>length</c>/<c>isEmpty</c> —
/// the latter resolve directly in <see cref="VirtualMachine"/>'s <c>GetProperty</c> arm
/// and never reach <c>GetMethod</c>, so caching them would prove nothing about this
/// cache. All chunks are hand-constructed; no compiler dependency, mirroring the
/// sibling query/mutating member test files.
/// </summary>
public sealed class VirtualMachineReceiverMethodCacheTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) => (byte)chunk.AddConstant(value);

    // -----------------------------------------------------------------------
    // GetMethod identity: the cache mechanism itself, tested directly against
    // ArrayNatives/MapNatives — no VM execution needed.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayGetMethod_SameNameSameReceiver_ReturnsSameCachedInstance() {
        var receiver = new GrobArray([]);
        NativeFunction? first = ArrayNatives.GetMethod("contains", receiver);
        NativeFunction? second = ArrayNatives.GetMethod("contains", receiver);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void ArrayGetMethod_SameNameDifferentReceivers_ReturnsDistinctInstances() {
        var a = new GrobArray([]);
        var b = new GrobArray([]);
        NativeFunction? onA = ArrayNatives.GetMethod("contains", a);
        NativeFunction? onB = ArrayNatives.GetMethod("contains", b);

        Assert.NotNull(onA);
        Assert.NotNull(onB);
        Assert.NotSame(onA, onB);
    }

    [Fact]
    public void ArrayGetMethod_DifferentNamesSameReceiver_EachCachedIndependently() {
        var receiver = new GrobArray([]);
        NativeFunction? containsMethod = ArrayNatives.GetMethod("contains", receiver);
        NativeFunction? filterMethod = ArrayNatives.GetMethod("filter", receiver);

        Assert.NotNull(containsMethod);
        Assert.NotNull(filterMethod);
        Assert.NotSame(containsMethod, filterMethod);
        Assert.Same(containsMethod, ArrayNatives.GetMethod("contains", receiver));
        Assert.Same(filterMethod, ArrayNatives.GetMethod("filter", receiver));
    }

    [Fact]
    public void ArrayGetMethod_UnrecognisedName_StaysAMiss_RegardlessOfCacheState() {
        var receiver = new GrobArray([]);
        Assert.Null(ArrayNatives.GetMethod("bogus", receiver));

        // Populate the cache with a real method, then re-check the miss — caching a
        // hit must never turn an unrelated miss into a hit.
        ArrayNatives.GetMethod("contains", receiver);
        Assert.Null(ArrayNatives.GetMethod("bogus", receiver));
    }

    [Fact]
    public void MapGetMethod_SameNameSameReceiver_ReturnsSameCachedInstance() {
        var receiver = new GrobMap();
        NativeFunction? first = MapNatives.GetMethod("get", receiver);
        NativeFunction? second = MapNatives.GetMethod("get", receiver);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void MapGetMethod_SameNameDifferentReceivers_ReturnsDistinctInstances() {
        var a = new GrobMap();
        var b = new GrobMap();
        NativeFunction? onA = MapNatives.GetMethod("get", a);
        NativeFunction? onB = MapNatives.GetMethod("get", b);

        Assert.NotNull(onA);
        Assert.NotNull(onB);
        Assert.NotSame(onA, onB);
    }

    [Fact]
    public void MapGetMethod_UnrecognisedName_StaysAMiss_RegardlessOfCacheState() {
        var receiver = new GrobMap();
        Assert.Null(MapNatives.GetMethod("bogus", receiver));

        MapNatives.GetMethod("get", receiver);
        Assert.Null(MapNatives.GetMethod("bogus", receiver));
    }

    // -----------------------------------------------------------------------
    // Load-bearing: cache correctness under mutation (D-393 Q2 reason (a)). Two
    // separate VM.Run() calls share the SAME receiver, so the second dispatch hits
    // the cache the first one populated.
    // -----------------------------------------------------------------------

    private static Chunk BuildContainsCallChunk(GrobArray array, GrobValue needle) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(array)), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("contains")), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, needle), 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Fact]
    public void Contains_CachedBoundMethod_ReflectsMutationBetweenCalls() {
        var xs = new GrobArray([GrobValue.FromInt(1), GrobValue.FromInt(2)]);

        var (vm1, _) = NewVm();
        vm1.Run(BuildContainsCallChunk(xs, GrobValue.FromInt(3)));
        Assert.False(vm1.Stack.Peek().AsBool());

        xs.Add(GrobValue.FromInt(3));

        var (vm2, _) = NewVm();
        vm2.Run(BuildContainsCallChunk(xs, GrobValue.FromInt(3)));
        Assert.True(vm2.Stack.Peek().AsBool());
    }

    /// <summary>Single-parameter lambda — <c>x =&gt; x &gt; 1</c>.</summary>
    private static BytecodeFunction BuildGreaterThanOneLambda() {
        var fnChunk = new Chunk();
        int oneIdx = fnChunk.AddConstant(GrobValue.FromInt(1));
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)oneIdx, 1);
        fnChunk.WriteOpCode(OpCode.GreaterInt, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        fnChunk.WriteOpCode(OpCode.Nil, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        return new BytecodeFunction("", 1, fnChunk);
    }

    private static Chunk BuildFilterCallChunk(GrobArray array, BytecodeFunction predicate) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(array)), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("filter")), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromFunction(predicate)), 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Fact]
    public void Filter_CachedBoundMethod_ReflectsMutationBetweenCalls() {
        var xs = new GrobArray([GrobValue.FromInt(1), GrobValue.FromInt(2)]);
        BytecodeFunction predicate = BuildGreaterThanOneLambda(); // x => x > 1

        var (vm1, _) = NewVm();
        vm1.Run(BuildFilterCallChunk(xs, predicate));
        Assert.True(vm1.Stack.Peek().TryAsArray(out GrobArray? firstResult));
        Assert.Equal(1, firstResult!.Count); // only [2]

        xs.Add(GrobValue.FromInt(3));

        var (vm2, _) = NewVm();
        vm2.Run(BuildFilterCallChunk(xs, predicate));
        Assert.True(vm2.Stack.Peek().TryAsArray(out GrobArray? secondResult));
        Assert.Equal(2, secondResult!.Count); // [2, 3]
    }

    private static Chunk BuildMapGetCallChunk(GrobMap map, string key) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("get")), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(key)), 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Fact]
    public void Get_CachedBoundMethod_ReflectsMutationBetweenCalls() {
        var map = new GrobMap();

        var (vm1, _) = NewVm();
        vm1.Run(BuildMapGetCallChunk(map, "a"));
        Assert.True(vm1.Stack.Peek().IsNil);

        map.Set("a", GrobValue.FromInt(42));

        var (vm2, _) = NewVm();
        vm2.Run(BuildMapGetCallChunk(map, "a"));
        Assert.Equal(GrobValue.FromInt(42), vm2.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // Load-bearing: per-access VM context stays per-access (D-393 Q2 reason (b)). The
    // SAME array + method name is invoked from two different call sites across two
    // separate runs, so the second dispatch hits the cache the first run populated —
    // yet each uncaught fault must report its OWN call site's line/column, not the
    // site that first populated the cache. Mirrors
    // VirtualMachineNativeTests.Filter_LambdaFault_Uncaught_ReportsOriginalCallSiteLineAndColumn.
    // -----------------------------------------------------------------------

    private static Chunk BuildFaultingFilterChunk(
            GrobArray array, NativeFunction faultingPredicate, int line, int column) {
        var chunk = new Chunk();
        int arrIdx = chunk.AddConstant(GrobValue.FromArray(array));
        chunk.WriteOpCode(OpCode.Constant, line, column);
        chunk.WriteByte((byte)arrIdx, line, column);

        int propIdx = chunk.AddConstant(GrobValue.FromString("filter"));
        chunk.WriteOpCode(OpCode.GetProperty, line, column);
        chunk.WriteByte((byte)propIdx, line, column);

        int lambdaIdx = chunk.AddConstant(GrobValue.FromFunction(faultingPredicate));
        chunk.WriteOpCode(OpCode.Constant, line, column);
        chunk.WriteByte((byte)lambdaIdx, line, column);

        chunk.WriteOpCode(OpCode.Call, line, column);
        chunk.WriteByte(1, line, column);
        chunk.WriteOpCode(OpCode.Nil, line, column);
        chunk.WriteOpCode(OpCode.Return, line, column);
        return chunk;
    }

    [Fact]
    public void Filter_CachedAcrossCalls_FaultReportsEachCallSitesOwnLineAndColumn() {
        var xs = new GrobArray([GrobValue.FromInt(1)]);
        var faultingPredicate = new NativeFunction("faultingPredicate", 1,
            (_, _) => throw new NativeFaultException(
                "ArithmeticError", ErrorCatalog.E5006.Code, "predicate faulted"));

        var (vm1, _) = NewVm();
        GrobRuntimeException firstEx = Assert.Throws<GrobRuntimeException>(
            () => vm1.Run(BuildFaultingFilterChunk(xs, faultingPredicate, line: 5, column: 12)));
        Assert.Equal(5, firstEx.Line);
        Assert.Equal(12, firstEx.Column);

        // A different VM instance, same xs + "filter" — hits the cache entry
        // GrobArray.GetCachedMethod resolves from the first run, but from a
        // DIFFERENT call site.
        var (vm2, _) = NewVm();
        GrobRuntimeException secondEx = Assert.Throws<GrobRuntimeException>(
            () => vm2.Run(BuildFaultingFilterChunk(xs, faultingPredicate, line: 20, column: 7)));
        Assert.Equal(20, secondEx.Line);
        Assert.Equal(7, secondEx.Column);
    }
}
