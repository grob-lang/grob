using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 9 Increment C0b-2a (D-377) — the map non-mutating query
/// member surface: the <c>length</c>/<c>isEmpty</c>/<c>keys</c>/<c>values</c> properties
/// (<see cref="OpCode.GetProperty"/>) and the <c>get</c>/<c>contains</c> methods (<see
/// cref="MapNatives.GetMethod"/>). All chunks are hand-constructed; no compiler
/// dependency, mirroring <c>VirtualMachineArrayQueryMemberTests</c>.
/// </summary>
public sealed class VirtualMachineMapQueryMemberTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) =>
        (byte)chunk.AddConstant(value);

    private static GrobMap BuildMap(params (string Key, GrobValue Value)[] entries) {
        var map = new GrobMap();
        foreach ((string key, GrobValue value) in entries) map.Set(key, value);
        return map;
    }

    /// <summary>Map constant, <c>GetProperty &lt;propertyName&gt;</c>, Return.</summary>
    private static Chunk BuildPropertyChunk(GrobMap map, string propertyName) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(propertyName)), 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    /// <summary>
    /// Map constant, <c>GetProperty &lt;methodName&gt;</c>, the argument, Call 1, Return —
    /// the shape <c>get(key)</c>/<c>contains(key)</c> compile to.
    /// </summary>
    private static Chunk BuildOneArgMethodChunk(GrobMap map, string methodName, GrobValue argument) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(methodName)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, argument), 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    // -----------------------------------------------------------------------
    // length / isEmpty
    // -----------------------------------------------------------------------

    [Fact]
    public void Length_NonEmptyMap_ReturnsEntryCount() {
        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk(BuildMap(("a", GrobValue.FromInt(1)), ("b", GrobValue.FromInt(2))), "length"));

        Assert.Equal(2, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Length_EmptyMap_ReturnsZero() {
        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk(new GrobMap(), "length"));

        Assert.Equal(0, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void IsEmpty_EmptyMap_ReturnsTrue() {
        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk(new GrobMap(), "isEmpty"));

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void IsEmpty_NonEmptyMap_ReturnsFalse() {
        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk(BuildMap(("a", GrobValue.FromInt(1))), "isEmpty"));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // keys / values — insertion order, including survival of the mutation path.
    // -----------------------------------------------------------------------

    [Fact]
    public void Keys_ReturnsKeysInInsertionOrder() {
        var (vm, _) = NewVm();
        GrobMap map = BuildMap(("z", GrobValue.FromInt(1)), ("a", GrobValue.FromInt(2)), ("m", GrobValue.FromInt(3)));
        vm.Run(BuildPropertyChunk(map, "keys"));

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(["z", "a", "m"], [result![0].AsString(), result[1].AsString(), result[2].AsString()]);
    }

    [Fact]
    public void Values_IsIndexAlignedWithKeys() {
        var (vm, _) = NewVm();
        GrobMap map = BuildMap(("z", GrobValue.FromInt(1)), ("a", GrobValue.FromInt(2)), ("m", GrobValue.FromInt(3)));
        vm.Run(BuildPropertyChunk(map, "values"));

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(1, result![0].AsInt());
        Assert.Equal(2, result[1].AsInt());
        Assert.Equal(3, result[2].AsInt());
    }

    [Fact]
    public void Keys_MapBuiltViaSuccessiveIndexAssignment_PreservesInsertionOrder() {
        // Mirrors 'm["z"] = 1; m["a"] = 2' — proves ordering survives the mutation
        // (indexer-write) path, not only literal construction.
        var map = new GrobMap();
        map["z"] = GrobValue.FromInt(1);
        map["a"] = GrobValue.FromInt(2);

        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk(map, "keys"));

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(["z", "a"], [result![0].AsString(), result[1].AsString()]);
    }

    [Fact]
    public void Values_EmptyMap_ReturnsEmptyArray() {
        var (vm, _) = NewVm();
        vm.Run(BuildPropertyChunk(new GrobMap(), "values"));

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? result));
        Assert.Equal(0, result!.Count);
    }

    // -----------------------------------------------------------------------
    // get(key) — agrees with the indexer: nil on absent key, value on present key.
    // -----------------------------------------------------------------------

    [Fact]
    public void Get_KeyPresent_ReturnsValue() {
        var (vm, _) = NewVm();
        vm.Run(BuildOneArgMethodChunk(BuildMap(("a", GrobValue.FromInt(42))), "get", GrobValue.FromString("a")));

        Assert.Equal(42, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Get_KeyAbsent_ReturnsNil() {
        var (vm, _) = NewVm();
        vm.Run(BuildOneArgMethodChunk(BuildMap(("a", GrobValue.FromInt(42))), "get", GrobValue.FromString("z")));

        Assert.True(vm.Stack.Peek().IsNil);
    }

    // -----------------------------------------------------------------------
    // contains(key) — key membership.
    // -----------------------------------------------------------------------

    [Fact]
    public void Contains_KeyPresent_ReturnsTrue() {
        var (vm, _) = NewVm();
        vm.Run(BuildOneArgMethodChunk(BuildMap(("a", GrobValue.FromInt(1))), "contains", GrobValue.FromString("a")));

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Contains_KeyAbsent_ReturnsFalse() {
        var (vm, _) = NewVm();
        vm.Run(BuildOneArgMethodChunk(BuildMap(("a", GrobValue.FromInt(1))), "contains", GrobValue.FromString("z")));

        Assert.False(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Contains_EmptyMap_ReturnsFalse() {
        var (vm, _) = NewVm();
        vm.Run(BuildOneArgMethodChunk(new GrobMap(), "contains", GrobValue.FromString("a")));

        Assert.False(vm.Stack.Peek().AsBool());
    }
}
