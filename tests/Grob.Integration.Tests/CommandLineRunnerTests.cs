using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// <see cref="CommandLineRunner"/> is the argv parsing and subcommand dispatch that used to
/// sit in <c>Program.cs</c>, outside coverage and untested (<c>docs/dev/coverage-policy.md</c>).
/// Each test pins one branch of today's surface — <c>run</c>, <c>repl</c>, help,
/// <c>--verbose</c> and the two usage errors — with the exact stdout, stderr and exit code
/// <c>Program.cs</c> produced.
/// </summary>
public sealed class CommandLineRunnerTests {
    private static readonly string NL = Environment.NewLine;

    private static (string Stdout, string Stderr, int ExitCode) Run(
            string[] args, TextReader? stdin = null) {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = new CommandLineRunner(stdin ?? TextReader.Null, stdout, stderr).Run(args);
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunScript(
            string source, params string[] argsAroundPath) {
        string path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            string[] args = [.. argsAroundPath.Select(a => a == "{path}" ? path : a)];
            return Run(args);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void NoArguments_PrintsHelp_ExitsZero() {
        (string stdout, string stderr, int exitCode) = Run([]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Matches(@"^Grob \S+\r?\n\r?\nUsage:\r?\n", stdout);
        Assert.Contains("  grob run <file>    Run a .grob script" + NL, stdout);
        Assert.Contains("  grob repl          Start the interactive REPL" + NL, stdout);
        Assert.EndsWith("  --verbose    Start log.* at the debug threshold" + NL, stdout);
    }

    [Fact]
    public void HelpFlag_PrintsTheSameHelpAsNoArguments() {
        (string helpOut, string helpErr, int helpExit) = Run(["--help"]);
        (string bareOut, _, _) = Run([]);

        Assert.Equal(0, helpExit);
        Assert.Equal(string.Empty, helpErr);
        Assert.Equal(bareOut, helpOut);
    }

    [Fact]
    public void Run_WithoutAPath_ReportsMissingPath_ExitsThree() {
        (string stdout, string stderr, int exitCode) = Run(["run"]);

        Assert.Equal(3, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal(
            "error: missing file path" + NL + "usage: grob run <file> [--verbose]" + NL, stderr);
    }

    [Fact]
    public void UnknownCommand_ReportsUsage_ExitsThree() {
        (string stdout, string stderr, int exitCode) = Run(["frobnicate"]);

        Assert.Equal(3, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal("error: unknown command" + NL + "usage: grob run <file>" + NL, stderr);
    }

    [Fact]
    public void Run_WithAPath_RunsTheScript() {
        (string stdout, string stderr, int exitCode) =
            RunScript("print(\"hello\")\n", "run", "{path}");

        Assert.Equal(0, exitCode);
        Assert.Equal("hello" + NL, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void Run_WithoutVerbose_SuppressesLogDebug() {
        (string stdout, string stderr, int exitCode) =
            RunScript("log.debug(\"hidden\")\nprint(\"done\")\n", "run", "{path}");

        Assert.Equal(0, exitCode);
        Assert.Equal("done" + NL, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Theory]
    [InlineData("--verbose", "run", "{path}")]
    [InlineData("run", "--verbose", "{path}")]
    [InlineData("run", "{path}", "--verbose")]
    public void Verbose_AnywhereInArgv_IsStrippedAndEnablesLogDebug(string a, string b, string c) {
        (string stdout, string stderr, int exitCode) =
            RunScript("log.debug(\"visible\")\nprint(\"done\")\n", a, b, c);

        Assert.Equal(0, exitCode);
        Assert.Equal("done" + NL, stdout);
        Assert.Equal("visible" + NL, stderr);
    }

    [Fact]
    public void VerboseAlone_PrintsHelp() {
        (string verboseOut, string stderr, int exitCode) = Run(["--verbose"]);
        (string bareOut, _, _) = Run([]);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal(bareOut, verboseOut);
    }

    [Fact]
    public void Repl_ReadsFromTheInjectedStdin() {
        (string stdout, string stderr, int exitCode) =
            Run(["repl"], new StringReader("print(\"from repl\")\n"));

        Assert.Equal(0, exitCode);
        Assert.Contains("from repl" + NL, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Theory]
    [InlineData("1.2.3+abc123", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("0.9.0-preview.4+sha.deadbeef", "0.9.0-preview.4")]
    [InlineData(null, "unknown")]
    public void CliVersion_DropsTheBuildMetadataSuffix(string? informational, string expected) {
        Assert.Equal(expected, CommandLineRunner.CliVersion(informational));
    }
}
