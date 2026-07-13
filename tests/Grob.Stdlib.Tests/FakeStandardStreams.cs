using Grob.Runtime;

namespace Grob.Stdlib.Tests;

/// <summary>
/// An in-memory <see cref="IStandardStreams"/> test double, shared by every stdlib plugin
/// fixture that needs one (<see cref="LogPlugin"/>, <see cref="IoPlugin"/>) — mirrors
/// <c>Grob.Vm.Tests</c>'s own private <c>FakeStreams</c>, but declared here since that one
/// is private to its test class and this project cannot reference <c>Grob.Vm.Tests</c>.
/// <see cref="In"/> defaults to an empty <see cref="StringReader"/> (immediate EOF,
/// <c>ReadLine()</c> returns <see langword="null"/>) unless a fixture supplies real input.
/// </summary>
internal sealed class FakeStandardStreams : IStandardStreams {
    public TextWriter Out { get; }
    public TextWriter Error { get; }
    public TextReader In { get; }

    internal FakeStandardStreams(TextWriter? output = null, TextWriter? error = null, TextReader? input = null) {
        Out = output ?? new StringWriter();
        Error = error ?? new StringWriter();
        In = input ?? new StringReader(string.Empty);
    }
}
