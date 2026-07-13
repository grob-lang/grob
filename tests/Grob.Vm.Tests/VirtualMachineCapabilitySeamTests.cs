using Grob.Core;
using Grob.Runtime;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM tests for Sprint 8 Increment A's capability-injection seam (D-343):
/// <see cref="VirtualMachine"/>'s <see cref="IStandardStreams"/> constructor overload
/// routes <see cref="OpCode.Print"/> through the injected stream, and
/// <see cref="VirtualMachine.RegisterConstant"/> — the <see cref="IPluginRegistrar"/>
/// surface's constant-registration half — makes a value retrievable by qualified name.
/// All chunks are hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineCapabilitySeamTests {
    private sealed class FakeStreams : IStandardStreams {
        public TextWriter Out { get; } = new StringWriter();
        public TextWriter Error { get; } = new StringWriter();
        public TextReader In { get; } = TextReader.Null;
    }

    private static Chunk BuildPrintChunk(GrobValue value) {
        var chunk = new Chunk();
        int idx = chunk.AddConstant(value);
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)idx, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Fact]
    public void IStandardStreamsConstructor_Print_WritesToInjectedOutStream() {
        var streams = new FakeStreams();
        var vm = new VirtualMachine(streams);

        vm.Run(BuildPrintChunk(GrobValue.FromString("hello")));

        Assert.Equal("hello" + Environment.NewLine, streams.Out.ToString());
        Assert.Equal("", streams.Error.ToString());
    }

    [Fact]
    public void IStandardStreamsConstructor_NullStreams_Throws() {
        Assert.Throws<ArgumentNullException>(() => new VirtualMachine((IStandardStreams)null!));
    }

    [Fact]
    public void TextWriterConstructor_Print_StillWritesToTheGivenWriter() {
        // Back-compat proof (D-343): the pre-existing single-TextWriter constructor's
        // observable behaviour is unchanged even though it now wraps a SingleWriterStreams
        // internally.
        var output = new StringWriter();
        var vm = new VirtualMachine(output);

        vm.Run(BuildPrintChunk(GrobValue.FromString("hello")));

        Assert.Equal("hello" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void RegisterConstant_AddsGlobalValue_RetrievableByGetGlobal() {
        var vm = new VirtualMachine(new StringWriter());
        vm.RegisterConstant("math.pi", GrobValue.FromFloat(3.141592653589793));

        var chunk = new Chunk();
        int nameIdx = chunk.AddConstant(GrobValue.FromString("math.pi"));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        vm.Run(chunk);

        Assert.Equal(GrobValue.FromFloat(3.141592653589793), vm.Stack.Peek());
    }

    [Fact]
    public void RegisterConstant_NullName_Throws() {
        var vm = new VirtualMachine(new StringWriter());
        Assert.Throws<ArgumentNullException>(() => vm.RegisterConstant(null!, GrobValue.FromInt(1)));
    }

    [Fact]
    public void VirtualMachine_ImplementsIPluginRegistrar() {
        var vm = new VirtualMachine(new StringWriter());
        Assert.IsAssignableFrom<IPluginRegistrar>(vm);
    }

    [Fact]
    public void SingleWriterStreams_Error_IsTextWriterNull() {
        var streams = new SingleWriterStreams(new StringWriter());
        Assert.Same(TextWriter.Null, streams.Error);
    }

    [Fact]
    public void SingleWriterStreams_In_IsClosedReader_ReadLineReturnsNullImmediately() {
        // Sprint 8 Increment C: the ~39 legacy `new VirtualMachine(writer)` call sites
        // have no real stdin — TextReader.Null.ReadLine() returning null immediately is
        // the correct "closed stream" behaviour input() must translate into IoError.
        var streams = new SingleWriterStreams(new StringWriter());
        Assert.Same(TextReader.Null, streams.In);
        Assert.Null(streams.In.ReadLine());
    }
}
