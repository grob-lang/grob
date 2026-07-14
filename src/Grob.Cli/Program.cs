using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Grob.Cli;

// --verbose (Sprint 8 Increment C) is a presence flag recognised anywhere in argv for
// both "run" and "repl" — stripped out before the existing positional dispatch below so
// neither command's own argument parsing (the file path for "run") needs to know about it.
bool verbose = args.Contains("--verbose");
string[] positional = [.. args.Where(a => a != "--verbose")];

// grob run <file>
if (positional.Length >= 1 && positional[0] == "run") {
    if (positional.Length < 2) {
        await Console.Error.WriteLineAsync("error: missing file path");
        await Console.Error.WriteLineAsync("usage: grob run <file> [--verbose]");
        return 1;
    }
    return new RunCommand(Console.Out, Console.Error, Console.In, verbose).Run(positional[1]);
}

// grob repl
if (positional.Length >= 1 && positional[0] == "repl") {
    return new ReplCommand(Console.In, Console.Out, Console.Error, verbose).Run();
}

// grob / grob --help
if (positional.Length == 0 || positional[0] == "--help") {
    string informational = typeof(RunCommand).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "unknown";
    int plusIdx = informational.IndexOf('+');
    string cliVersion = plusIdx >= 0 ? informational[..plusIdx] : informational;
    Console.WriteLine($"Grob {cliVersion}");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  grob run <file>    Run a .grob script");
    Console.WriteLine("  grob repl          Start the interactive REPL");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --help       Show this message");
    Console.WriteLine("  --verbose    Start log.* at the debug threshold");
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
