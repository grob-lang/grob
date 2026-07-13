using Grob.Core;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment C: <see cref="EnvPlugin"/> registers <c>env.get</c>, <c>env.require</c>,
/// <c>env.has</c>, <c>env.set</c> and <c>env.all</c> via <see cref="IGrobPlugin"/>, end to end
/// through a real <see cref="VirtualMachine"/> against an injected <see cref="FakeEnvironment"/>
/// (D-343 — no direct OS access from <c>Grob.Stdlib</c>). Chunks are hand-constructed — this
/// project has no dependency on <c>Grob.Compiler</c>.
/// </summary>
public sealed class EnvPluginTests {
    private static VirtualMachine NewRegisteredVm(FakeEnvironment env) {
        var vm = new VirtualMachine(new StringWriter());
        new EnvPlugin(env).Register(vm);
        return vm;
    }

    [Fact]
    public void Name_IsEnv() {
        Assert.Equal("env", new EnvPlugin(new FakeEnvironment()).Name);
    }

    [Fact]
    public void Register_AddsExactlyTheDocumentedEnvMembers() {
        var vm = NewRegisteredVm(new FakeEnvironment());

        string[] expectedMembers = ["get", "require", "has", "set", "all"];
        foreach (string member in expectedMembers) {
            Assert.True(vm.Globals.ContainsKey($"env.{member}"), $"missing env.{member}");
        }
        Assert.Equal(expectedMembers.Length, vm.Globals.Count);
    }

    [Fact]
    public void Register_NullEnvironment_Throws() {
        Assert.Throws<ArgumentNullException>(() => new EnvPlugin(null!));
    }

    // -----------------------------------------------------------------------
    // env.get
    // -----------------------------------------------------------------------

    [Fact]
    public void Get_UnsetKey_ReturnsNil() {
        var vm = NewRegisteredVm(new FakeEnvironment());
        vm.Run(BuildCallChunk("env.get", GrobValue.FromString("MISSING")));

        Assert.True(vm.Stack.Peek().IsNil);
    }

    [Fact]
    public void Get_SetKey_ReturnsValue() {
        var vm = NewRegisteredVm(new FakeEnvironment(new Dictionary<string, string> { ["HOME"] = "/home/grob" }));
        vm.Run(BuildCallChunk("env.get", GrobValue.FromString("HOME")));

        Assert.Equal(GrobValue.FromString("/home/grob"), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // env.require
    // -----------------------------------------------------------------------

    [Fact]
    public void Require_SetKey_ReturnsValue() {
        var vm = NewRegisteredVm(new FakeEnvironment(new Dictionary<string, string> { ["HOME"] = "/home/grob" }));
        vm.Run(BuildCallChunk("env.require", GrobValue.FromString("HOME")));

        Assert.Equal(GrobValue.FromString("/home/grob"), vm.Stack.Peek());
    }

    [Fact]
    public void Require_AbsentKey_ThrowsLookupErrorNamingTheVariable() {
        var vm = NewRegisteredVm(new FakeEnvironment());

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("env.require", GrobValue.FromString("MISSING"))));

        Assert.Equal(ErrorCatalog.E5801.Code, ex.Code);
        Assert.Contains("MISSING", ex.Message);
    }

    [Fact]
    public void Require_EmptyKey_ThrowsLookupErrorNamingTheVariable() {
        var vm = NewRegisteredVm(new FakeEnvironment(new Dictionary<string, string> { ["EMPTY"] = "" }));

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("env.require", GrobValue.FromString("EMPTY"))));

        Assert.Equal(ErrorCatalog.E5801.Code, ex.Code);
        Assert.Contains("EMPTY", ex.Message);
    }

    [Fact]
    public void Require_AbsentKey_InsideTryCatch_IsCatchableLookupError() {
        // Proves the native-throw seam end to end: unwinds through the same
        // handler-table walk a user throw uses, not a bespoke path.
        var script = new Chunk();
        int calleeIdx = script.AddConstant(GrobValue.FromString("env.require"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.GetGlobal, 2); script.WriteByte((byte)calleeIdx, 2);
        int argIdx = script.AddConstant(GrobValue.FromString("MISSING"));
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
            [new CatchHandler(["LookupError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var vm = NewRegisteredVm(new FakeEnvironment());
        vm.Run(script);

        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal("LookupError", s!.TypeName);
    }

    // -----------------------------------------------------------------------
    // env.has
    // -----------------------------------------------------------------------

    [Fact]
    public void Has_AbsentKey_ReturnsFalse() {
        var vm = NewRegisteredVm(new FakeEnvironment());
        vm.Run(BuildCallChunk("env.has", GrobValue.FromString("MISSING")));

        Assert.Equal(GrobValue.FromBool(false), vm.Stack.Peek());
    }

    [Fact]
    public void Has_EmptyKey_ReturnsFalse() {
        var vm = NewRegisteredVm(new FakeEnvironment(new Dictionary<string, string> { ["EMPTY"] = "" }));
        vm.Run(BuildCallChunk("env.has", GrobValue.FromString("EMPTY")));

        Assert.Equal(GrobValue.FromBool(false), vm.Stack.Peek());
    }

    [Fact]
    public void Has_SetNonEmptyKey_ReturnsTrue() {
        var vm = NewRegisteredVm(new FakeEnvironment(new Dictionary<string, string> { ["HOME"] = "/home/grob" }));
        vm.Run(BuildCallChunk("env.has", GrobValue.FromString("HOME")));

        Assert.Equal(GrobValue.FromBool(true), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // env.set
    // -----------------------------------------------------------------------

    [Fact]
    public void Set_ThenGet_SeesTheNewValue() {
        var env = new FakeEnvironment();
        var vm = NewRegisteredVm(env);

        vm.Run(BuildCallChunk("env.set", GrobValue.FromString("NEW_VAR"), GrobValue.FromString("value")));
        Assert.True(vm.Stack.Peek().IsNil);

        vm.Run(BuildCallChunk("env.get", GrobValue.FromString("NEW_VAR")));
        Assert.Equal(GrobValue.FromString("value"), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // env.all
    // -----------------------------------------------------------------------

    [Fact]
    public void All_ReturnsEveryEntryAsAMap() {
        var seed = new Dictionary<string, string> { ["A"] = "1", ["B"] = "2" };
        var vm = NewRegisteredVm(new FakeEnvironment(seed));
        vm.Run(BuildCallChunk("env.all"));

        Assert.True(vm.Stack.Peek().TryAsMap(out GrobMap? map));
        Assert.Equal(2, map!.Entries.Count);
        Assert.Equal(GrobValue.FromString("1"), map.Entries["A"]);
        Assert.Equal(GrobValue.FromString("2"), map.Entries["B"]);
    }

    [Fact]
    public void All_EmptyEnvironment_ReturnsEmptyMap() {
        var vm = NewRegisteredVm(new FakeEnvironment());
        vm.Run(BuildCallChunk("env.all"));

        Assert.True(vm.Stack.Peek().TryAsMap(out GrobMap? map));
        Assert.Empty(map!.Entries);
    }
}
