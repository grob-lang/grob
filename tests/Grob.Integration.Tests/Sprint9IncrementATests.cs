using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 Increment A integration tests — the D-345 array-indexer emission
/// fix. Driven through <see cref="RunCommand"/> so the full pipeline (lex,
/// parse, type-check, compile, VM) and the CLI's top-level diagnostic
/// formatting are all exercised end-to-end, which is what actually proves the
/// regression: before this increment, <c>arr[i]</c> had no compiler emission
/// at all and crashed the VM, so no real <c>.grob</c> script could reach
/// either the successful-read or the out-of-range path.
/// </summary>
public sealed class Sprint9IncrementATests {
    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) =>
        Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-9a", name);

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    [Fact]
    public void ArrayIndexRead_ReturnsElementsAndChainsForMultiDimensional() {
        (string stdout, string stderr, int exitCode) = RunFile("array-index-read.grob");

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(
            "10" + NL + "20" + NL + "30" + NL + // arr[0], arr[1], arr[2]
            "2" + NL + "3" + NL,                 // matrix[0][1], matrix[1][0]
            stdout);
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void ArrayIndex_OutOfRange_Unhandled_ProducesTopLevelDiagnosticAndExitsOne() {
        (string stdout, string stderr, int exitCode) = RunFile("array-index-out-of-range.grob");

        Assert.Equal(string.Empty, stdout);
        Assert.StartsWith("error[E5101]:", stderr);
        Assert.Contains("out of range", stderr);
        Assert.Contains("array-index-out-of-range.grob:8", stderr);
        Assert.Equal(1, exitCode);
    }
}
