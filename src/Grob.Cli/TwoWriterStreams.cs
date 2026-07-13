using Grob.Runtime;

namespace Grob.Cli;

/// <summary>
/// The composition root's <see cref="IStandardStreams"/> implementation (D-343) —
/// wraps the three streams <see cref="RunCommand"/>/<see cref="ReplCommand"/> already
/// receive (<see cref="Console.Out"/>/<see cref="Console.Error"/>/<see cref="Console.In"/>
/// in production; in-memory writers/readers in tests) with no OS access of its own. Named
/// for the original two-writer shape (Increment A); Increment C adds the stdin reader
/// <see cref="In"/> without renaming the type, to keep the diff minimal.
/// </summary>
internal sealed class TwoWriterStreams : IStandardStreams {
    public TextWriter Out { get; }
    public TextWriter Error { get; }
    public TextReader In { get; }

    internal TwoWriterStreams(TextWriter stdout, TextWriter stderr, TextReader stdin) {
        Out = stdout;
        Error = stderr;
        In = stdin;
    }
}
