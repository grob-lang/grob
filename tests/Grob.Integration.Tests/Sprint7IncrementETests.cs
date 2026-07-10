using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 7 Increment E integration tests — the sprint-close smoke script that
/// exercises the complete §27 exception-handling surface: <c>throw</c> with a
/// matching typed catch, a non-matching typed catch proving source-order
/// first-match, the catch-all catching what typed catches miss, <c>finally</c>
/// on both normal and exceptional completion, a nested <c>try</c>/<c>finally</c>
/// early return running both finallys inner-then-outer (D-334), a caught
/// runtime error (int division by zero as <c>ArithmeticError</c>), and an
/// uncatchable <c>exit()</c> inside <c>try</c>/<c>catch</c>/<c>finally</c>.
/// </summary>
public sealed class Sprint7IncrementETests {
    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) =>
        Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-7e", name);

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    [Fact]
    public void ErrorsSmoke_RunsAndProducesExpectedOutput() {
        (string stdout, string stderr, int exitCode) = RunFile("errors.grob");

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(
            "caught IoError: config file missing" + NL +
            "caught ArithmeticError: step value out of range" + NL +
            "caught by catch-all: malformed payload" + NL +
            "try body completed normally" + NL +
            "finally ran after normal completion" + NL +
            "caught IndexError: index 10 out of range for length 3" + NL +
            "finally ran after exceptional completion" + NL +
            "inner finally" + NL +
            "outer finally" + NL +
            "nested result: 1" + NL +
            "caught runtime ArithmeticError: integer division by zero" + NL +
            "10 / 0 -> 0" + NL +
            "10 / 2 -> 5" + NL +
            "before exit" + NL,
            stdout);
        // exit(42) inside try/catch/finally is uncatchable (D-110): neither
        // the catch nor the finally runs, and the process terminates with
        // the given code — the departure from the prior four smoke scripts'
        // exit(0).
        Assert.Equal(42, exitCode);
    }
}
