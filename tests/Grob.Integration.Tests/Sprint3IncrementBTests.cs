using System.Text;
using Grob.Cli;
using Grob.Core;
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
        Assert.Contains("type-error.grob:5:6", stderr);
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
        Assert.Contains("runtime-error.grob:5", stderr); // E5002 does not report a column
    }

    // -----------------------------------------------------------------------
    // Missing file / unreadable path
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

    [Fact]
    public void DirectoryNotFound_WritesStderrAndExitsNonZero() {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        // Path inside a non-existent directory triggers DirectoryNotFoundException.
        int exitCode = new RunCommand(stdout, stderr).Run(
            Path.Join("no-such-dir", "no-such-file.grob"));

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout.ToString());
        Assert.Contains("no-such-file.grob", stderr.ToString());
    }

    // -----------------------------------------------------------------------
    // exit(code) — compiled as Constant + OpCode.Exit (QA pass sprint 3)
    // -----------------------------------------------------------------------

    [Fact]
    public void ExitBuiltin_SetsProcessExitCode() {
        // Fixture: exit(2) — RunCommand must return 2.
        (string stdout, string stderr, int exitCode) = RunFile("exit-code.grob");

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // DiagnosticFormatter.WriteRuntime — column path
    // -----------------------------------------------------------------------

    [Fact]
    public void WriteRuntime_WithColumn_IncludesColumnInLocation() {
        // The compiler currently records column = 0 for all instructions, so the
        // ex.Column > 0 branch in DiagnosticFormatter.WriteRuntime is unreachable
        // through a compiled .grob file.  Exercise it directly with a crafted
        // GrobRuntimeException that carries a non-zero column.
        using var writer = new StringWriter(new StringBuilder());
        var ex = new GrobRuntimeException("E5002", 3, 7, "integer division by zero");

        DiagnosticFormatter.WriteRuntime(ex, "script.grob", writer);

        string output = writer.ToString();
        Assert.Contains("E5002", output);
        Assert.Contains("script.grob:3:7", output);
    }

    [Fact]
    public void WriteRuntime_WithoutColumn_OmitsColumnFromLocation() {
        // Column = 0 → location string is "file:line" with no column suffix.
        using var writer = new StringWriter(new StringBuilder());
        var ex = new GrobRuntimeException("E5002", 5, "integer division by zero");

        DiagnosticFormatter.WriteRuntime(ex, "script.grob", writer);

        string output = writer.ToString();
        Assert.Contains("E5002", output);
        Assert.Contains("script.grob:5", output);
        Assert.DoesNotContain("script.grob:5:", output);
    }
}
