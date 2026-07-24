using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 9 Increment C0a-1 (D-371) — the array non-mutating
/// query member surface: the <c>length</c>/<c>isEmpty</c> properties (<see
/// cref="OpCode.GetProperty"/>), the <c>first</c>/<c>last</c>/<c>contains</c> methods
/// (<see cref="ArrayNatives.GetMethod"/>), and <see cref="GrobValueComparer"/>'s new
/// <c>date</c>/<c>guid</c> comparer arms proven against <see cref="OpCode.LessDate"/>
/// directly so the two comparison paths cannot silently diverge. All chunks are
/// hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineArrayQueryMemberTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) =>
        (byte)chunk.AddConstant(value);

    private static GrobValue Date(DateTimeOffset value) => DateNatives.FromDateTimeOffset(value);

    private static GrobValue Guid(string canonical) => GuidNatives.FromCanonicalString(canonical);

    /// <summary>Array constant, <c>GetProperty &lt;propertyName&gt;</c>, Return.</summary>
    private static Chunk BuildPropertyChunk(GrobValue[] elements, string propertyName) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(new GrobArray(elements))), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(propertyName)), 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    /// <summary>Array constant, <c>GetProperty &lt;methodName&gt;</c>, Call 0, Return.</summary>
    private static Chunk BuildZeroArgMethodChunk(GrobValue[] elements, string methodName) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(new GrobArray(elements))), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(methodName)), 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    /// <summary>
    /// Array constant, <c>GetProperty &lt;methodName&gt;</c>, the argument, Call 1,
    /// Return — the shape <c>contains(v)</c> compiles to.
    /// </summary>
    private static Chunk BuildOneArgMethodChunk(GrobValue[] elements, string methodName, GrobValue argument) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(new GrobArray(elements))), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(methodName)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, argument), 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    /// <summary>Array constant, GetProperty "sort", the key-selector lambda, Call 1, Return.</summary>
    private static Chunk BuildSortChunk(GrobValue[] elements, BytecodeFunction keyFn) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromArray(new GrobArray(elements))), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("sort")), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromFunction(keyFn)), 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    /// <summary>A single-parameter identity lambda — <c>x =&gt; x</c> — used as the key
    /// selector when the element itself is the sort key.</summary>
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
    /// A single-parameter lambda — <c>x =&gt; x / divisor</c> (truncating int division) —
    /// used to project a group key out of an encoded <c>(key * divisor) + id</c> element,
    /// so a stable sort's relative-order guarantee is observable on otherwise-identical
    /// key values.
    /// </summary>
    private static BytecodeFunction BuildDivideLambda(long divisor) {
        var fnChunk = new Chunk();
        int divisorIdx = fnChunk.AddConstant(GrobValue.FromInt(divisor));
        fnChunk.WriteOpCode(OpCode.GetLocal, 1);
        fnChunk.WriteByte(0, 1);
        fnChunk.WriteOpCode(OpCode.Constant, 1);
        fnChunk.WriteByte((byte)divisorIdx, 1);
        fnChunk.WriteOpCode(OpCode.DivideInt, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        fnChunk.WriteOpCode(OpCode.Nil, 1);
        fnChunk.WriteOpCode(OpCode.Return, 1);
        return new BytecodeFunction("", 1, fnChunk);
    }

    private static bool RunLessDate(DateTimeOffset a, DateTimeOffset b) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, Date(a)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ConstByte(chunk, Date(b)), 1);
        chunk.WriteOpCode(OpCode.LessDate, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);
        return vm.Stack.Peek().AsBool();
    }

    // -----------------------------------------------------------------------
    // length / isEmpty
    // -----------------------------------------------------------------------

    [Fact]
    public void Length_NonEmptyArray_ReturnsCount() {
        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk([GrobValue.FromInt(1), GrobValue.FromInt(2), GrobValue.FromInt(3)], "length"));

        Assert.Equal(3, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Length_EmptyArray_ReturnsZero() {
        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk([], "length"));

        Assert.Equal(0, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void IsEmpty_EmptyArray_ReturnsTrue() {
        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk([], "isEmpty"));

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void IsEmpty_NonEmptyArray_ReturnsFalse() {
        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk([GrobValue.FromInt(1)], "isEmpty"));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // first() / last()
    // -----------------------------------------------------------------------

    [Fact]
    public void First_NonEmptyArray_ReturnsFirstElement() {
        var (vm, _) = NewVm();
        vm.Run(BuildZeroArgMethodChunk(
            [GrobValue.FromInt(10), GrobValue.FromInt(20), GrobValue.FromInt(30)], "first"));

        Assert.Equal(GrobValue.FromInt(10), vm.Stack.Peek());
    }

    [Fact]
    public void First_EmptyArray_ReturnsNil() {
        var (vm, _) = NewVm();
        vm.Run(BuildZeroArgMethodChunk([], "first"));

        Assert.True(vm.Stack.Peek().IsNil);
    }

    [Fact]
    public void Last_NonEmptyArray_ReturnsLastElement() {
        var (vm, _) = NewVm();
        vm.Run(BuildZeroArgMethodChunk(
            [GrobValue.FromInt(10), GrobValue.FromInt(20), GrobValue.FromInt(30)], "last"));

        Assert.Equal(GrobValue.FromInt(30), vm.Stack.Peek());
    }

    [Fact]
    public void Last_EmptyArray_ReturnsNil() {
        var (vm, _) = NewVm();
        vm.Run(BuildZeroArgMethodChunk([], "last"));

        Assert.True(vm.Stack.Peek().IsNil);
    }

    [Fact]
    public void Last_SingleElementArray_ReturnsThatElement() {
        var (vm, _) = NewVm();
        vm.Run(BuildZeroArgMethodChunk([GrobValue.FromInt(42)], "last"));

        Assert.Equal(GrobValue.FromInt(42), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // contains(v)
    // -----------------------------------------------------------------------

    [Fact]
    public void Contains_ElementPresent_ReturnsTrue() {
        var (vm, _) = NewVm();
        GrobValue[] elements = [GrobValue.FromString("a"), GrobValue.FromString("b"), GrobValue.FromString("c")];
        vm.Run(BuildOneArgMethodChunk(elements, "contains", GrobValue.FromString("b")));

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Contains_ElementAbsent_ReturnsFalse() {
        var (vm, _) = NewVm();
        GrobValue[] elements = [GrobValue.FromString("a"), GrobValue.FromString("b")];
        vm.Run(BuildOneArgMethodChunk(elements, "contains", GrobValue.FromString("z")));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Contains_EmptyArray_ReturnsFalse() {
        var (vm, _) = NewVm();
        vm.Run(BuildOneArgMethodChunk([], "contains", GrobValue.FromInt(1)));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // contains(v) — date equality parity (D-371's load-bearing test). contains must
    // never disagree with '==' — D-367 made date equality instant-based, so a needle
    // at a different UTC offset but the SAME instant as an array element must still
    // be found.
    // -----------------------------------------------------------------------

    [Fact]
    public void Contains_DateArray_SameInstantDifferentOffset_ReturnsTrue() {
        var atPlusOne = new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.FromHours(1));
        var atUtc = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero); // same instant

        var (vm, _) = NewVm();
        GrobValue[] elements = [Date(atPlusOne)];
        vm.Run(BuildOneArgMethodChunk(elements, "contains", Date(atUtc)));

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Contains_DateArray_DifferentInstant_ReturnsFalse() {
        var d1 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var d2 = d1.AddDays(1);

        var (vm, _) = NewVm();
        GrobValue[] elements = [Date(d1)];
        vm.Run(BuildOneArgMethodChunk(elements, "contains", Date(d2)));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // sort — date/guid comparer arms (GrobValueComparer, D-371).
    // -----------------------------------------------------------------------

    [Fact]
    public void Sort_ByDateKey_OrdersByInstant_MatchingLessDateSemantics() {
        var d1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);          // 00:00 UTC
        var d2 = new DateTimeOffset(2026, 1, 1, 13, 0, 0, TimeSpan.FromHours(2)); // 11:00 UTC
        var d3 = new DateTimeOffset(2026, 1, 2, 9, 0, 0, TimeSpan.FromHours(-5)); // 14:00 UTC (Jan 2)

        // Cross-check against LessDate directly (D-371) — the two comparison paths
        // must not be able to silently diverge.
        Assert.True(RunLessDate(d1, d2));
        Assert.True(RunLessDate(d2, d3));

        var (vm, _) = NewVm();
        GrobValue[] elements = [Date(d3), Date(d1), Date(d2)];
        vm.Run(BuildSortChunk(elements, BuildIdentityLambda()));

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(Date(d1), result![0]);
        Assert.Equal(Date(d2), result[1]);
        Assert.Equal(Date(d3), result[2]);
    }

    [Fact]
    public void Sort_ByGuidKey_OrdersOrdinally() {
        GrobValue g1 = Guid("11111111-1111-1111-1111-111111111111");
        GrobValue g2 = Guid("22222222-2222-2222-2222-222222222222");
        GrobValue g3 = Guid("33333333-3333-3333-3333-333333333333");

        var (vm, _) = NewVm();
        GrobValue[] elements = [g3, g1, g2];
        vm.Run(BuildSortChunk(elements, BuildIdentityLambda()));

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(g1, result![0]);
        Assert.Equal(g2, result[1]);
        Assert.Equal(g3, result[2]);
    }

    [Fact]
    public void Sort_StabilityPreserved_EqualKeysRetainInputOrder() {
        // Elements encode (key * 100) + id; the key-selector lambda projects the key
        // via truncating int division, so elements sharing a key are otherwise
        // distinguishable and a stable sort's relative-order guarantee is observable.
        GrobValue[] elements = [
            GrobValue.FromInt(201), // key 2, id 1
            GrobValue.FromInt(101), // key 1, id 1
            GrobValue.FromInt(102), // key 1, id 2
            GrobValue.FromInt(202), // key 2, id 2
            GrobValue.FromInt(103), // key 1, id 3
        ];

        var (vm, _) = NewVm();
        vm.Run(BuildSortChunk(elements, BuildDivideLambda(100)));

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(5, result!.Count);
        Assert.Equal(GrobValue.FromInt(101), result[0]);
        Assert.Equal(GrobValue.FromInt(102), result[1]);
        Assert.Equal(GrobValue.FromInt(103), result[2]);
        Assert.Equal(GrobValue.FromInt(201), result[3]);
        Assert.Equal(GrobValue.FromInt(202), result[4]);
    }

    // -----------------------------------------------------------------------
    // sort — still-uncomparable kinds (E0004 unchanged).
    // -----------------------------------------------------------------------

    [Fact]
    public void Sort_ByArrayKey_ThrowsE0004() {
        GrobValue[] elements = [
            GrobValue.FromArray(new GrobArray([GrobValue.FromInt(1)])),
            GrobValue.FromArray(new GrobArray([GrobValue.FromInt(2)])),
        ];

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildSortChunk(elements, BuildIdentityLambda())));
        Assert.Equal("E0004", ex.Code);
    }

    [Fact]
    public void Sort_ByMapKey_ThrowsE0004() {
        GrobValue[] elements = [GrobValue.FromMap(new GrobMap()), GrobValue.FromMap(new GrobMap())];

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildSortChunk(elements, BuildIdentityLambda())));
        Assert.Equal("E0004", ex.Code);
    }

    [Fact]
    public void Sort_ByUserStructKey_ThrowsE0004() {
        GrobValue[] elements = [
            GrobValue.FromStruct(new GrobStruct("Config", [])),
            GrobValue.FromStruct(new GrobStruct("Config", [])),
        ];

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => vm.Run(BuildSortChunk(elements, BuildIdentityLambda())));
        Assert.Equal("E0004", ex.Code);
    }
}
