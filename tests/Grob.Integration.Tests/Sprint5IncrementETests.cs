using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 5 Increment E integration tests — the top-level initialisation state
/// machine (§19.1, D-294) and flow-sensitive narrowing. Driven through
/// <see cref="RunCommand"/> so stdout, stderr and the process exit code are all
/// observed end-to-end.
/// </summary>
public sealed class Sprint5IncrementETests {
    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) =>
        Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-5e", name);

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    // -----------------------------------------------------------------------
    // Flow-sensitive narrowing runs end-to-end: a nullable parameter narrowed
    // inside `if (x != nil)` is interpolated, and the un-narrowed branch falls
    // through to the default.
    // -----------------------------------------------------------------------

    [Fact]
    public void Narrowing_NullableParameter_PrintsNarrowedValue_AndExitsZero() {
        (string stdout, string stderr, int exitCode) = RunFile("narrowing.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"Hello, World{NL}Hello, stranger{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // An ordering/circular top-level initialisation — a binding's initialiser
    // calls a function whose declaration has not yet executed — fails with E5902
    // and exit code 1, and produces no stdout.
    // -----------------------------------------------------------------------

    [Fact]
    public void CircularInitialisation_FailsWithE5902_AndExitsOne() {
        (string stdout, string stderr, int exitCode) = RunFile("circular-init.grob");

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5902", stderr);
    }
}
