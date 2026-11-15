using System.Reflection;

namespace Grob.Cli;

/// <summary>
/// Parses <c>argv</c> and dispatches to a subcommand: <c>grob run &lt;file&gt;</c>,
/// <c>grob repl</c> and help. <c>Program.cs</c> builds one over the console streams and
/// returns its exit code, so every branch of the command line is here, in scope for
/// coverage and unit tested (<c>docs/dev/coverage-policy.md</c>).
/// </summary>
/// <remarks>
/// <c>--verbose</c> is a presence flag recognised anywhere in <c>argv</c> for both
/// <c>run</c> and <c>repl</c>. It is stripped before the positional dispatch, so neither
/// command's own argument parsing needs to know about it.
/// </remarks>
internal class CommandLineRunner {
    private const string VerboseFlag = "--verbose";

    private readonly TextReader _stdin;
    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;

    /// <summary>Creates a runner over the given standard streams.</summary>
    /// <param name="stdin">Source for <c>input()</c> under <c>run</c>, and the REPL's input.</param>
    /// <param name="stdout">Writer for programme output and help.</param>
    /// <param name="stderr">Writer for diagnostics and usage errors.</param>
    public CommandLineRunner(TextReader stdin, TextWriter stdout, TextWriter stderr) {
        _stdin = stdin;
        _stdout = stdout;
        _stderr = stderr;
    }

    /// <summary>Runs the command <paramref name="args"/> names.</summary>
    /// <returns>The process exit code.</returns>
    public int Run(string[] args) {
        bool verbose = args.Contains(VerboseFlag);
        string[] positional = [.. args.Where(a => a != VerboseFlag)];

        if (positional.Length == 0 || positional[0] == "--help") {
            return WriteHelp();
        }

        return positional[0] switch {
            "run" => RunScript(positional, verbose),
            "repl" => new ReplCommand(_stdin, _stdout, _stderr, verbose).Run(),
            _ => UsageError("error: unknown command", "usage: grob run <file>"),
        };
    }

    /// <summary>
    /// The CLI version from an assembly's informational version, with any <c>+</c> build
    /// metadata dropped; <c>unknown</c> when the attribute is absent.
    /// </summary>
    internal static string CliVersion(string? informational) {
        if (informational is null) {
            return "unknown";
        }
        int plusIdx = informational.IndexOf('+');
        return plusIdx >= 0 ? informational[..plusIdx] : informational;
    }

    private int RunScript(string[] positional, bool verbose) {
        if (positional.Length < 2) {
            return UsageError("error: missing file path", "usage: grob run <file> [--verbose]");
        }
        return new RunCommand(_stdout, _stderr, _stdin, verbose).Run(positional[1]);
    }

    private int WriteHelp() {
        string? informational = typeof(RunCommand).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        _stdout.WriteLine($"Grob {CliVersion(informational)}");
        _stdout.WriteLine();
        _stdout.WriteLine("Usage:");
        _stdout.WriteLine("  grob run <file>    Run a .grob script");
        _stdout.WriteLine("  grob repl          Start the interactive REPL");
        _stdout.WriteLine();
        _stdout.WriteLine("Options:");
        _stdout.WriteLine("  --help       Show this message");
        _stdout.WriteLine("  --verbose    Start log.* at the debug threshold");
        return 0;
    }

    private int UsageError(string message, string usage) {
        _stderr.WriteLine(message);
        _stderr.WriteLine(usage);
        return 1;
    }
}
