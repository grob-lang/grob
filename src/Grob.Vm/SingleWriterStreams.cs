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
/// <see cref="In"/> is <see cref="TextReader.Null"/> — a closed stream, mirroring
/// <see cref="Error"/>'s "nothing wired yet" pattern; <see cref="TextReader.Null"/>'s
/// <c>ReadLine()</c> returns <see langword="null"/> immediately, the correct behaviour
/// for <c>input()</c> (Increment C) against a legacy call site with no real stdin.
/// </summary>
internal sealed class SingleWriterStreams : IStandardStreams {
    public TextWriter Out { get; }
    public TextWriter Error => TextWriter.Null;
    public TextReader In => TextReader.Null;

    internal SingleWriterStreams(TextWriter output) => Out = output;
}
