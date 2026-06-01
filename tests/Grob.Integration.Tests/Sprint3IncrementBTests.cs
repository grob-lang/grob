using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 3 Increment B integration tests: <c>grob run</c> — the CLI wrapper
/// that compiles and executes a <c>.grob</c> file through the existing pipeline.
/// Tests drive through <see cref="RunCommand"/> with captured stdout/stderr writers.
/// </summary>
public sealed class Sprint3IncrementBTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) {
        if (Path.IsPathRooted(name))
            throw new ArgumentException($"Fixture name must be a relative path, got: {name}", nameof(name));
        return Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-3b", name);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void HappyPath_DeclaresReassignsAndPrints_WritesExpectedStdoutAndExitsZero() {
        (string stdout, string stderr, int exitCode) = RunFile("hello-run.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal($"10{NL}hello world{NL}3.14{NL}", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // Compile-time error — no execution
    // -----------------------------------------------------------------------

    [Fact]
    public void CompileTimeTypeError_WritesStderrAndExitsNonZeroWithNoStdout() {
        (string stdout, string stderr, int exitCode) = RunFile("type-error.grob");

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0002", stderr);
    }

    // -----------------------------------------------------------------------
    // Runtime error
    // -----------------------------------------------------------------------

    [Fact]
    public void RuntimeError_DivisionByZero_WritesStderrAndExitsNonZero() {
        (string stdout, string stderr, int exitCode) = RunFile("runtime-error.grob");

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5002", stderr);
    }

    // -----------------------------------------------------------------------
    // Missing file
    // -----------------------------------------------------------------------

    [Fact]
    public void MissingFile_WritesStderrMessageAndExitsNonZero() {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run("does-not-exist.grob");

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("does-not-exist.grob", stderr.ToString());
    }

    // -----------------------------------------------------------------------
    // exit(code) — deferred: requires exit() function support (Sprint 5+)
    // -----------------------------------------------------------------------

    [Fact(Skip = "exit() function call requires Call opcode support (Sprint 5). Infrastructure (GrobExitException) is in place; fixture deferred.")]
    public void ExitBuiltin_SetsProcessExitCode() {
        // When exit() is implemented, a fixture containing `exit(2)` should
        // cause RunCommand.Run to return 2.
        Assert.Fail("Test body deferred to Sprint 5.");
    }
}
