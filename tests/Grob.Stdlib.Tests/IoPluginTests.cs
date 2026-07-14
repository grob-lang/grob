using Grob.Core;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment A gave <see cref="IoPlugin"/> the print/exit formalisation
/// placeholder role (D-343) — <c>print</c>/<c>exit</c> stay on their existing dedicated
/// opcodes. Sprint 8 Increment C gives it its first real callable: the bare (no-namespace)
/// <c>input</c> native, registered against the injected <see cref="IStandardStreams"/>
/// (writes the prompt to <see cref="IStandardStreams.Out"/> with no trailing newline, reads
/// one line from <see cref="IStandardStreams.In"/>). A <see langword="null"/> line — stdin
/// closed or exhausted — is translated into a catchable <c>IoError</c> via the native-throw
/// seam (D-342) rather than a bespoke path.
/// </summary>
public sealed class IoPluginTests {
    [Fact]
    public void Name_IsIo() {
        Assert.Equal("io", new IoPlugin(new FakeStandardStreams()).Name);
    }

    [Fact]
    public void Register_RegistersExactlyInput() {
        var streams = new FakeStandardStreams();
        var vm = new VirtualMachine(streams);
        new IoPlugin(streams).Register(vm);

        Assert.True(vm.Globals.ContainsKey("input"));
        Assert.Single(vm.Globals);
    }

    [Fact]
    public void Register_NullRegistrar_Throws() {
        Assert.Throws<ArgumentNullException>(() => new IoPlugin(new FakeStandardStreams()).Register(null!));
    }

    [Fact]
    public void Constructor_NullStreams_Throws() {
        Assert.Throws<ArgumentNullException>(() => new IoPlugin(null!));
    }

    // -----------------------------------------------------------------------
    // input() — prompt write, line read.
    // -----------------------------------------------------------------------

    [Fact]
    public void Input_WritesPromptWithNoTrailingNewline_ReturnsTheNextLine() {
        var streams = new FakeStandardStreams(input: new StringReader("Ada\nmore\n"));
        var vm = new VirtualMachine(streams);
        new IoPlugin(streams).Register(vm);

        vm.Run(BuildCallChunk("input", GrobValue.FromString("Name: ")));

        Assert.Equal("Name: ", streams.Out.ToString());
        Assert.Equal(GrobValue.FromString("Ada"), vm.Stack.Peek());
    }

    [Fact]
    public void Input_EmptyPrompt_WritesNothingToStdout_StillReadsLine() {
        var streams = new FakeStandardStreams(input: new StringReader("value\n"));
        var vm = new VirtualMachine(streams);
        new IoPlugin(streams).Register(vm);

        vm.Run(BuildCallChunk("input", GrobValue.FromString("")));

        Assert.Equal(string.Empty, streams.Out.ToString());
        Assert.Equal(GrobValue.FromString("value"), vm.Stack.Peek());
    }

    [Fact]
    public void Input_ReturnedLine_HasNoTrailingNewline() {
        var streams = new FakeStandardStreams(input: new StringReader("no-newline-in-value"));
        var vm = new VirtualMachine(streams);
        new IoPlugin(streams).Register(vm);

        vm.Run(BuildCallChunk("input", GrobValue.FromString("")));

        Assert.Equal("no-newline-in-value", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void Input_ClosedStdin_ThrowsIoError() {
        var streams = new FakeStandardStreams(input: TextReader.Null);
        var vm = new VirtualMachine(streams);
        new IoPlugin(streams).Register(vm);

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("input", GrobValue.FromString("Name: "))));

        Assert.Equal(ErrorCatalog.E5305.Code, ex.Code);
    }

    [Fact]
    public void Input_ClosedStdin_InsideTryCatch_IsCatchableIoError() {
        var script = new Chunk();
        int calleeIdx = script.AddConstant(GrobValue.FromString("input"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.GetGlobal, 2); script.WriteByte((byte)calleeIdx, 2);
        int argIdx = script.AddConstant(GrobValue.FromString(""));
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
            [new CatchHandler(["IoError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var streams = new FakeStandardStreams(input: TextReader.Null);
        var vm = new VirtualMachine(streams);
        new IoPlugin(streams).Register(vm);
        vm.Run(script);

        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal("IoError", s!.TypeName);
    }
}
