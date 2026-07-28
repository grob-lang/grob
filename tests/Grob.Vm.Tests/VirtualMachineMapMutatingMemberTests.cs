using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 9 Increment C0b-2b (D-378) — the map in-place-mutating
/// member surface: <c>set</c>, <c>remove</c>, <c>clear</c> (<see cref="MapNatives.GetMethod"/>).
/// All three mutate the receiver <see cref="GrobMap"/> in place and return
/// <see cref="GrobValue.Nil"/>. All chunks are hand-constructed; no compiler dependency,
/// mirroring <c>VirtualMachineArrayMutatingMemberTests</c>. Also pins the load-bearing
/// insertion-order guarantee (new key appended last, overwritten key's position
/// unchanged), the <c>remove</c>-on-absent-key no-op (vs. the array's throwing
/// <c>remove(index)</c>), the <c>set</c>/<c>[k] = v</c> write-path agreement (both funnel
/// through <see cref="GrobMap.Set"/>), and D-372's reference-semantics ratification.
/// </summary>
public sealed class VirtualMachineMapMutatingMemberTests {
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

    /// <summary>Map constant, <c>GetProperty &lt;methodName&gt;</c>, each argument in
    /// order, <c>Call &lt;argCount&gt;</c>, Return — the shape every map method call
    /// compiles to.</summary>
    private static Chunk BuildMethodChunk(GrobMap map, string methodName, params GrobValue[] arguments) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(methodName)), 1);
        foreach (GrobValue argument in arguments) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte(ConstByte(chunk, argument), 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)arguments.Length, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    /// <summary>Receiver, key, value, <c>SetIndex</c>, Return — the shape <c>m[k] = v</c>
    /// compiles to (D-350).</summary>
    private static Chunk BuildSetIndexChunk(GrobMap map, string key, GrobValue value) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromMap(map)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(key)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, value), 1);
        chunk.WriteOpCode(OpCode.SetIndex, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    /// <summary>
    /// <c>first.set(key, value)</c> through the first reference, discards the nil result,
    /// then loads the second reference and returns it on the stack — the two references
    /// are separate constants in the pool (<c>AddConstant</c> never dedupes), so this
    /// models two distinct Grob-visible bindings over one map, not one CLR alias.
    /// </summary>
    private static Chunk BuildSetThroughFirstThenLoadSecond(
            GrobValue first, GrobValue second, string key, GrobValue value) {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, first), 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString("set")), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, GrobValue.FromString(key)), 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, value), 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Pop, 1);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(ConstByte(chunk, second), 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    // -----------------------------------------------------------------------
    // set(key, value)
    // -----------------------------------------------------------------------

    [Fact]
    public void Set_NewKey_InsertsValue_AndReturnsNil() {
        GrobMap map = BuildMap(("a", GrobValue.FromInt(1)));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(map, "set", GrobValue.FromString("b"), GrobValue.FromInt(2)));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.True(map.TryGetValue("b", out GrobValue value));
        Assert.Equal(2, value.AsInt());
    }

    [Fact]
    public void Set_ExistingKey_OverwritesValue_AndReturnsNil() {
        GrobMap map = BuildMap(("a", GrobValue.FromInt(1)));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(map, "set", GrobValue.FromString("a"), GrobValue.FromInt(99)));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.True(map.TryGetValue("a", out GrobValue value));
        Assert.Equal(99, value.AsInt());
    }

    // -----------------------------------------------------------------------
    // Ordering — load-bearing (D-377's guarantee, which `set` must not violate). A new
    // key appends last; overwriting an existing key leaves its position unchanged.
    // -----------------------------------------------------------------------

    [Fact]
    public void Set_NewKey_AppearsLastInInsertionOrder() {
        GrobMap map = BuildMap(("z", GrobValue.FromInt(1)), ("a", GrobValue.FromInt(2)));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(map, "set", GrobValue.FromString("new"), GrobValue.FromInt(3)));

        Assert.Equal(["z", "a", "new"], map.InsertionOrderKeys);
        Assert.Equal([1, 2, 3], [.. map.InsertionOrderValues.Select(v => v.AsInt())]);
    }

    [Fact]
    public void Set_ExistingKey_PreservesItsOriginalPosition() {
        GrobMap map = BuildMap(("z", GrobValue.FromInt(1)), ("a", GrobValue.FromInt(2)), ("m", GrobValue.FromInt(3)));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(map, "set", GrobValue.FromString("a"), GrobValue.FromInt(99)));

        Assert.Equal(["z", "a", "m"], map.InsertionOrderKeys);
        Assert.Equal([1, 99, 3], [.. map.InsertionOrderValues.Select(v => v.AsInt())]);
    }

    // -----------------------------------------------------------------------
    // set / SetIndex agreement — both funnel through GrobMap.Set, so they can never
    // drift. Proven for both a new key and an existing key.
    // -----------------------------------------------------------------------

    [Fact]
    public void Set_AndSetIndex_ProduceIdenticalState_ForNewKey() {
        GrobMap viaMethod = BuildMap(("z", GrobValue.FromInt(1)));
        GrobMap viaIndex = BuildMap(("z", GrobValue.FromInt(1)));

        var (vm1, _) = NewVm();
        vm1.Run(BuildMethodChunk(viaMethod, "set", GrobValue.FromString("new"), GrobValue.FromInt(2)));
        var (vm2, _) = NewVm();
        vm2.Run(BuildSetIndexChunk(viaIndex, "new", GrobValue.FromInt(2)));

        Assert.Equal(viaIndex.InsertionOrderKeys, viaMethod.InsertionOrderKeys);
        Assert.Equal(
            [.. viaIndex.InsertionOrderValues.Select(v => v.AsInt())],
            [.. viaMethod.InsertionOrderValues.Select(v => v.AsInt())]);
    }

    [Fact]
    public void Set_AndSetIndex_ProduceIdenticalState_ForExistingKey() {
        GrobMap viaMethod = BuildMap(("z", GrobValue.FromInt(1)), ("a", GrobValue.FromInt(2)));
        GrobMap viaIndex = BuildMap(("z", GrobValue.FromInt(1)), ("a", GrobValue.FromInt(2)));

        var (vm1, _) = NewVm();
        vm1.Run(BuildMethodChunk(viaMethod, "set", GrobValue.FromString("z"), GrobValue.FromInt(99)));
        var (vm2, _) = NewVm();
        vm2.Run(BuildSetIndexChunk(viaIndex, "z", GrobValue.FromInt(99)));

        Assert.Equal(viaIndex.InsertionOrderKeys, viaMethod.InsertionOrderKeys);
        Assert.Equal(
            [.. viaIndex.InsertionOrderValues.Select(v => v.AsInt())],
            [.. viaMethod.InsertionOrderValues.Select(v => v.AsInt())]);
    }

    // -----------------------------------------------------------------------
    // remove(key) — no-op if absent, the OPPOSITE of the array's bounds-checked,
    // throwing remove(index) (D-373).
    // -----------------------------------------------------------------------

    [Fact]
    public void Remove_ExistingKey_RemovesEntry_AndReturnsNil() {
        GrobMap map = BuildMap(("a", GrobValue.FromInt(1)), ("b", GrobValue.FromInt(2)));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(map, "remove", GrobValue.FromString("a")));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.False(map.TryGetValue("a", out _));
        Assert.Equal(["b"], map.InsertionOrderKeys);
    }

    [Fact]
    public void Remove_AbsentKey_IsNoOp_NoThrow_LengthUnchanged() {
        GrobMap map = BuildMap(("a", GrobValue.FromInt(1)));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(map, "remove", GrobValue.FromString("nope")));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.Single(map.InsertionOrderKeys);
        Assert.True(map.TryGetValue("a", out GrobValue value));
        Assert.Equal(1, value.AsInt());
    }

    [Fact]
    public void Remove_OnEmptyMap_IsNoOp_NoThrow() {
        var map = new GrobMap();
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(map, "remove", GrobValue.FromString("anything")));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.Empty(map.InsertionOrderKeys);
    }

    // -----------------------------------------------------------------------
    // clear()
    // -----------------------------------------------------------------------

    [Fact]
    public void Clear_EmptiesMap_AndReturnsNil() {
        GrobMap map = BuildMap(("a", GrobValue.FromInt(1)), ("b", GrobValue.FromInt(2)));
        var (vm, _) = NewVm();
        vm.Run(BuildMethodChunk(map, "clear"));

        Assert.True(vm.Stack.Peek().IsNil);
        Assert.Empty(map.InsertionOrderKeys);
        Assert.Empty(map.InsertionOrderValues);
    }

    // -----------------------------------------------------------------------
    // Aliasing — D-372's ratification, pinned directly (not incidentally).
    // -----------------------------------------------------------------------

    [Fact]
    public void Set_ThroughOneGrobReference_VisibleThroughAnother() {
        // a := map<...>{...}; b := a; a.set(k, v) — under reference semantics (D-372) a
        // and b wrap the SAME GrobMap, so the mutation made through a is observable
        // through b. The two references are modelled as two independent GrobValues —
        // separate constants in the pool — not a CLR-level `alias = shared`, which would
        // be a tautology. The mutation runs through the first constant; the second is
        // loaded by the VM itself and observed on the stack.
        GrobMap shared = BuildMap(("a", GrobValue.FromInt(1)));
        GrobValue referenceA = GrobValue.FromMap(shared);
        GrobValue referenceB = GrobValue.FromMap(shared);

        var (vm, _) = NewVm();
        vm.Run(BuildSetThroughFirstThenLoadSecond(referenceA, referenceB, "b", GrobValue.FromInt(2)));

        GrobValue observedThroughB = vm.Stack.Peek();
        Assert.True(observedThroughB.TryAsMap(out GrobMap? throughB));
        Assert.True(throughB!.TryGetValue("b", out GrobValue value));
        Assert.Equal(2, value.AsInt());

        // Both references denote the one underlying instance — reference, not value.
        Assert.True(referenceA.TryAsMap(out GrobMap? throughA));
        Assert.Same(throughA, throughB);
    }
}
