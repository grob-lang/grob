using Grob.Runtime;

namespace Grob.Cli;

/// <summary>
/// The composition root's <see cref="IStandardStreams"/> implementation (D-343) —
/// wraps the two <see cref="TextWriter"/>s <see cref="RunCommand"/>/<see cref="ReplCommand"/>
/// already receive (<see cref="Console.Out"/>/<see cref="Console.Error"/> in production;
/// in-memory writers in tests) with no OS access of its own.
/// </summary>
internal sealed class TwoWriterStreams : IStandardStreams {
    public TextWriter Out { get; }
    public TextWriter Error { get; }

    internal TwoWriterStreams(TextWriter stdout, TextWriter stderr) {
        Out = stdout;
        Error = stderr;
    }
}
