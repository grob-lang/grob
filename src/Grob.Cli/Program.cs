using System.Diagnostics.CodeAnalysis;

using Grob.Cli;

// grob run <file>
if (args.Length >= 1 && args[0] == "run") {
    if (args.Length < 2) {
        await Console.Error.WriteLineAsync("error: missing file path");
        await Console.Error.WriteLineAsync("usage: grob run <file>");
        return 1;
    }
    return new RunCommand(Console.Out, Console.Error).Run(args[1]);
}

// grob repl
if (args.Length >= 1 && args[0] == "repl") {
    return new ReplCommand(Console.In, Console.Out, Console.Error).Run();
}

// grob / grob --help
if (args.Length == 0 || args[0] == "--help") {
    Console.WriteLine("Grob 1.0.0");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  grob run <file>    Run a .grob script");
    Console.WriteLine("  grob repl          Start the interactive REPL");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --help    Show this message");
    return 0;
}

await Console.Error.WriteLineAsync("error: unknown command");
await Console.Error.WriteLineAsync("usage: grob run <file>");
return 1;


// Program.cs entry point — argv parsing and pipeline wiring.
// Exercised by end-to-end CLI tests, not unit tests.
// Excluded from coverage per project policy (see /docs/dev/coverage.md).
[ExcludeFromCodeCoverage(Justification = "CLI entry point; covered by integration tests. See docs/dev/coverage-policy.md.")]
internal partial class Program {
    protected Program() { }
}
