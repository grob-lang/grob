using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 8 Increment C integration tests: <c>env</c>, <c>log</c>, <c>input()</c> and the
/// <c>--verbose</c> CLI flag end to end through the full pipeline (lex -> parse ->
/// type-check -> compile -> VM, stdlib plugins auto-registered at startup), including the
/// <c>Grob.Cli</c> composition-root wiring this increment added (<see cref="TwoWriterStreams"/>'s
/// new stdin parameter, <see cref="SystemEnvironment"/>, <c>PluginRegistration.RegisterAll</c>'s
/// new signature).
/// </summary>
public sealed class Sprint8IncrementCTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(
            string source, TextReader? stdin = null, bool verbose = false) {
        // Build the temp path directly with the .grob extension: Path.GetTempFileName
        // creates (and leaves) a real 0-byte .tmp file on disk, and Path.ChangeExtension
        // only rewrites the string — so the .tmp would leak on every run.
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr, stdin, verbose).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    // -----------------------------------------------------------------------
    // TwoWriterStreams — stdin round-trip (Increment C).
    // -----------------------------------------------------------------------

    [Fact]
    public void TwoWriterStreams_In_ReturnsTheSuppliedReader() {
        var stdin = new StringReader("hello\n");
        var streams = new TwoWriterStreams(new StringWriter(), new StringWriter(), stdin);

        Assert.Same(stdin, streams.In);
    }

    // -----------------------------------------------------------------------
    // SystemEnvironment (Increment C) — real OS-backed IEnvironment.
    // -----------------------------------------------------------------------

    [Fact]
    public void SystemEnvironment_SetThenGet_RoundTrips() {
        var env = new SystemEnvironment();
        string key = $"GROB_TEST_VAR_{Guid.NewGuid():N}";
        try {
            Assert.Null(env.Get(key));
            Assert.False(env.Has(key));

            env.Set(key, "hello");

            Assert.Equal("hello", env.Get(key));
            Assert.True(env.Has(key));
            Assert.Equal("hello", env.All()[key]);
        } finally {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    // -----------------------------------------------------------------------
    // env module — end to end through RunCommand (Increment C).
    // -----------------------------------------------------------------------

    [Fact]
    public void EnvGet_RealProcessVariable_RoundTripsThroughRunCommand() {
        string key = $"GROB_TEST_VAR_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "grob-value");
        try {
            (string stdout, string stderr, int exitCode) = RunSource($"print(env.get(\"{key}\"))\n");

            Assert.Equal(0, exitCode);
            Assert.Equal("grob-value" + Environment.NewLine, stdout);
            Assert.Equal(string.Empty, stderr);
        } finally {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void EnvRequire_Missing_Unhandled_ExitsOneWithTopLevelDiagnostic() {
        (string stdout, string stderr, int exitCode) = RunSource(
            "env.require(\"GROB_DEFINITELY_UNSET_VARIABLE\")\n");

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5801", stderr);
    }

    // -----------------------------------------------------------------------
    // log module + --verbose (Increment C) — end to end through RunCommand.
    // -----------------------------------------------------------------------

    [Fact]
    public void LogDebug_WithoutVerbose_WritesNothingToStderr() {
        (string stdout, string stderr, int exitCode) =
            RunSource("log.debug(\"hidden\")\nprint(\"done\")\n", verbose: false);

        Assert.Equal(0, exitCode);
        Assert.Equal("done" + Environment.NewLine, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void LogDebug_WithVerbose_WritesToStderr_StdoutUnaffected() {
        (string stdout, string stderr, int exitCode) =
            RunSource("log.debug(\"visible\")\nprint(\"done\")\n", verbose: true);

        Assert.Equal(0, exitCode);
        Assert.Equal("done" + Environment.NewLine, stdout);
        Assert.Equal("visible" + Environment.NewLine, stderr);
    }

    // -----------------------------------------------------------------------
    // input() (Increment C) — end to end through RunCommand's stdin.
    // -----------------------------------------------------------------------

    [Fact]
    public void Input_PipedStdin_ReturnsTheNextLine_PromptGoesToStdout() {
        (string stdout, string stderr, int exitCode) =
            RunSource("""print(input("Name: "))""" + "\n", new StringReader("Ada\n"));

        Assert.Equal(0, exitCode);
        Assert.Equal("Name: Ada" + Environment.NewLine, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void Input_ClosedStdin_Unhandled_ExitsOneWithTopLevelDiagnostic() {
        (string stdout, string stderr, int exitCode) = RunSource("input()\n", stdin: null);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5305", stderr);
    }

    [Fact]
    public void Input_ClosedStdin_CaughtAsIoError_ScriptResumes() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            try {
                input()
            } catch (e: IoError) {
                print("caught: " + e.message)
            }
            print("done")
            """, stdin: null);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("caught: input(): stdin is closed", stdout);
        Assert.Contains("done", stdout);
    }
}
