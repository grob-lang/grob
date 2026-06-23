using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Integration tests for top-level initialisation order (D-321, §19.1): function
/// declarations are runtime-hoisted ahead of top-level code, pass-1 registers
/// top-level value bindings, and E5902 is narrowed to value-binding cycles.
/// Driven through <see cref="RunCommand"/> so stdout, stderr and the exit code are
/// observed end-to-end.
/// </summary>
public sealed class TopLevelInitialisationOrderTests {
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
    // A top-level statement that calls a function declared later in source
    // resolves and runs — functions are hoisted, so this is not E5902.
    // -----------------------------------------------------------------------

    [Fact]
    public void ForwardFunctionCall_ResolvesAndRuns_NoE5902() {
        (string stdout, string stderr, int exitCode) = RunFile("forward-call.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"hi{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // A function body reads a top-level value binding declared later in source.
    // Pass-1 registration resolves the read (no E1001); at call time the binding
    // is already initialised, so it runs.
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionReadsLaterDeclaredValue_ResolvesAndRuns_NoE1001() {
        (string stdout, string stderr, int exitCode) = RunFile("forward-value.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"hi{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // A genuine value-binding cycle (computeA/computeB) raises E5902 with a
    // trace-through-function message naming the initialising binding, the
    // function that read the uninitialised binding, and the binding read.
    // -----------------------------------------------------------------------

    [Fact]
    public void ValueBindingCycle_FailsWithE5902_TraceThroughFunction() {
        (string stdout, string stderr, int exitCode) = RunFile("circular-init.grob");

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5902", stderr);
        // The trace names the binding being initialised ('a'), the function that
        // performed the read ('computeA') and the uninitialised binding read ('b').
        Assert.Contains("'a'", stderr);
        Assert.Contains("computeA", stderr);
        Assert.Contains("'b'", stderr);
    }
}
