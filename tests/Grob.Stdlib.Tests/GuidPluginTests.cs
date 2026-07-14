using Grob.Core;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment D: <see cref="GuidPlugin"/> registers generation
/// (<c>newV4</c>/<c>newV7</c>/the variadic <c>newV5</c>), parsing (<c>parse</c>/
/// <c>tryParse</c>), the well-known namespaces and <c>empty</c>, and a registered
/// <c>toString()</c>, end to end through a real <see cref="VirtualMachine"/> against
/// fake <see cref="Grob.Runtime.IRandomSource"/>/<see cref="Grob.Runtime.IClock"/>.
/// Chunks are hand-constructed — this project has no dependency on <c>Grob.Compiler</c>.
/// </summary>
public sealed class GuidPluginTests {
    private static VirtualMachine NewRegisteredVm(
            Grob.Runtime.IRandomSource? randomSource = null, Grob.Runtime.IClock? clock = null) {
        var vm = new VirtualMachine(new StringWriter());
        new GuidPlugin(
            randomSource ?? new TestRandomSource(12345),
            clock ?? new TestClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        ).Register(vm);
        return vm;
    }

    private static Chunk BuildGetPropertyOnCallChunk(string calleeName, string propertyName, params GrobValue[] args) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString(calleeName));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)calleeIdx, 1);

        int[] argIndexes = [.. args.Select(chunk.AddConstant)];
        foreach (int argIdx in argIndexes) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte((byte)argIdx, 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)args.Length, 1);

        int propIdx = chunk.AddConstant(GrobValue.FromString(propertyName));
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte((byte)propIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    private static Chunk BuildMethodCallOnCallChunk(string calleeName, string methodName, params GrobValue[] args) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString(calleeName));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)calleeIdx, 1);

        int[] argIndexes = [.. args.Select(chunk.AddConstant)];
        foreach (int argIdx in argIndexes) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte((byte)argIdx, 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)args.Length, 1);

        int propIdx = chunk.AddConstant(GrobValue.FromString(methodName));
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte((byte)propIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Fact]
    public void Name_IsGuid() {
        Assert.Equal("guid", new GuidPlugin(new TestRandomSource(1), new TestClock(DateTime.UtcNow)).Name);
    }

    // -----------------------------------------------------------------------
    // Generation.
    // -----------------------------------------------------------------------

    [Fact]
    public void NewV4_Version_IsFour() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk("guid.newV4", "version"));

        Assert.Equal(4, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void NewV7_Version_IsSeven() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk("guid.newV7", "version"));

        Assert.Equal(7, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void NewV7_TwoSuccessiveCalls_AreTimeOrdered() {
        var clock = new TestClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var vm = NewRegisteredVm(clock: clock);

        vm.Run(BuildMethodCallOnCallChunk("guid.newV7", "toString"));
        string first = vm.Stack.Peek().AsString();

        clock.Now = clock.Now.AddMilliseconds(5);
        vm.Run(BuildMethodCallOnCallChunk("guid.newV7", "toString"));
        string second = vm.Stack.Peek().AsString();

        Assert.True(string.CompareOrdinal(first, second) < 0,
            $"expected '{first}' < '{second}' (time-ordered canonical strings)");
    }

    [Fact]
    public void NewV5_SameNamespaceAndNames_IsDeterministic() {
        var vm1 = NewRegisteredVm();
        vm1.Run(BuildGetGlobalChunk("guid.namespaces.url"));
        GrobValue ns = vm1.Stack.Peek();

        var chunk = new Chunk();
        int nsIdx = chunk.AddConstant(ns);
        int calleeIdx = chunk.AddConstant(GrobValue.FromString("guid.newV5"));
        int aIdx = chunk.AddConstant(GrobValue.FromString("a"));
        int bIdx = chunk.AddConstant(GrobValue.FromString("b"));

        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)calleeIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)nsIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)aIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)bIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(3, 1);
        int toStringIdx = chunk.AddConstant(GrobValue.FromString("toString"));
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte((byte)toStringIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var vm2 = NewRegisteredVm();
        vm2.Run(chunk);
        string first = vm2.Stack.Peek().AsString();

        var vm3 = NewRegisteredVm();
        vm3.Run(chunk);
        string second = vm3.Stack.Peek().AsString();

        Assert.Equal(first, second);
    }

    [Fact]
    public void NewV5_DifferentNames_ProducesDifferentGuid() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetGlobalChunk("guid.namespaces.url"));
        GrobValue ns = vm.Stack.Peek();

        static Chunk BuildNewV5(GrobValue ns, string name) {
            var chunk = new Chunk();
            int nsIdx = chunk.AddConstant(ns);
            int calleeIdx = chunk.AddConstant(GrobValue.FromString("guid.newV5"));
            int nameIdx = chunk.AddConstant(GrobValue.FromString(name));
            chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)calleeIdx, 1);
            chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)nsIdx, 1);
            chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)nameIdx, 1);
            chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(2, 1);
            int toStringIdx = chunk.AddConstant(GrobValue.FromString("toString"));
            chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte((byte)toStringIdx, 1);
            chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(0, 1);
            chunk.WriteOpCode(OpCode.Return, 1);
            return chunk;
        }

        var vmA = NewRegisteredVm();
        vmA.Run(BuildNewV5(ns, "a"));
        string first = vmA.Stack.Peek().AsString();

        var vmB = NewRegisteredVm();
        vmB.Run(BuildNewV5(ns, "b"));
        string second = vmB.Stack.Peek().AsString();

        Assert.NotEqual(first, second);
    }

    // -----------------------------------------------------------------------
    // Parsing.
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_ValidCanonicalString_RoundTrips() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk(
            "guid.parse", "toString", GrobValue.FromString("550e8400-e29b-41d4-a716-446655440000")));

        Assert.Equal("550e8400-e29b-41d4-a716-446655440000", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void Parse_InvalidString_ThrowsCatchableParseError() {
        var vm = NewRegisteredVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("guid.parse", GrobValue.FromString("not-a-guid"))));

        Assert.Equal(ErrorCatalog.E5701.Code, ex.Code);
    }

    [Fact]
    public void TryParse_ValidString_RoundTrips() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk(
            "guid.tryParse", "toString", GrobValue.FromString("550e8400-e29b-41d4-a716-446655440000")));

        Assert.Equal("550e8400-e29b-41d4-a716-446655440000", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void TryParse_InvalidString_ReturnsNil() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("guid.tryParse", GrobValue.FromString("not-a-guid")));

        Assert.True(vm.Stack.Peek().IsNil);
    }

    // -----------------------------------------------------------------------
    // Sentinel and namespaces.
    // -----------------------------------------------------------------------

    [Fact]
    public void Empty_IsEmpty_IsTrue() {
        var vm = NewRegisteredVm();
        var chunk = new Chunk();
        int emptyIdx = chunk.AddConstant(GrobValue.FromString("guid.empty"));
        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)emptyIdx, 1);
        int propIdx = chunk.AddConstant(GrobValue.FromString("isEmpty"));
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte((byte)propIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Namespaces_Dns_MatchesRfc4122Value() {
        var vm = NewRegisteredVm();
        var chunk = new Chunk();
        int idx = chunk.AddConstant(GrobValue.FromString("guid.namespaces.dns"));
        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)idx, 1);
        int propIdx = chunk.AddConstant(GrobValue.FromString("toString"));
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte((byte)propIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        vm.Run(chunk);

        Assert.Equal("6ba7b810-9dad-11d1-80b4-00c04fd430c8", vm.Stack.Peek().AsString());
    }

    // -----------------------------------------------------------------------
    // Display — toString/toUpperString/toCompactString.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("toString", "550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("toUpperString", "550E8400-E29B-41D4-A716-446655440000")]
    [InlineData("toCompactString", "550e8400e29b41d4a716446655440000")]
    public void ParsedGuid_RendersExpectedForm(string method, string expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk(
            "guid.parse", method, GrobValue.FromString("550e8400-e29b-41d4-a716-446655440000")));

        Assert.Equal(expected, vm.Stack.Peek().AsString());
    }

    [Fact]
    public void Print_RegisteredToString_RendersCanonicalString() {
        var vm = NewRegisteredVm();
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString("guid.parse"));
        int argIdx = chunk.AddConstant(GrobValue.FromString("550e8400-e29b-41d4-a716-446655440000"));
        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)calleeIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)argIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var output = new StringWriter();
        var freshVm = new VirtualMachine(output);
        new GuidPlugin(new TestRandomSource(1), new TestClock(DateTime.UtcNow)).Register(freshVm);
        freshVm.Run(chunk);

        Assert.Equal($"550e8400-e29b-41d4-a716-446655440000{Environment.NewLine}", output.ToString());
    }
}
