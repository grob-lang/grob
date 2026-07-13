using Grob.Runtime;

namespace Grob.Vm;

/// <summary>
/// Wraps a single <see cref="TextWriter"/> as an <see cref="IStandardStreams"/> for the
/// pre-existing <see cref="VirtualMachine(TextWriter, TextWriter?)"/> constructor
/// overload (D-343) — <see cref="Out"/> is the given writer; <see cref="Error"/> is
/// <see cref="TextWriter.Null"/>, since nothing in the VM writes to it yet (the <c>log</c>
/// module's stderr routing arrives in Increment C). Keeps the ~39 existing
/// <c>new VirtualMachine(writer)</c> call sites across the test suite and <c>Grob.Cli</c>
/// unchanged while <c>OpCode.Print</c> routes through the capability interface.
/// </summary>
internal sealed class SingleWriterStreams : IStandardStreams {
    public TextWriter Out { get; }
    public TextWriter Error => TextWriter.Null;

    internal SingleWriterStreams(TextWriter output) => Out = output;
}
