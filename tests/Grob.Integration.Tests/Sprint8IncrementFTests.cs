using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 8 Increment F integration tests — the sprint-close smoke script (D-337
/// family) that exercises the Sprint-8 stdlib surface with no Sprint-9 module:
/// <c>math</c>, <c>path.join</c>, an <c>env.require</c> caught as <c>LookupError</c>,
/// <c>log.info</c>/<c>log.error</c> to stderr, a <c>guid</c> generation/parse
/// round-trip through interpolation and <c>formatAs.table()</c> over a small struct
/// array.
/// </summary>
public sealed class Sprint8IncrementFTests {
    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) =>
        Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-8f", name);

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    [Fact]
    public void StdlibSmoke_RunsAndProducesExpectedOutput() {
        (string stdout, string stderr, int exitCode) = RunFile("stdlib.grob");

        // path.join follows Path.Combine (PathPlugin.cs), so the joined form uses
        // whichever separator the host OS gives Path.Combine — not a hard-coded
        // backslash, which only matched on Windows and failed this gold master on
        // the Linux CI runner. Path.Join (not Path.Combine) here for these three
        // fixed, always-relative literal segments — Path.Join never resets on a
        // rooted later argument, avoiding the CodeQL cs/path-injection concern
        // Path.Combine carries (same reasoning as CompileBenchmarks.cs), and it
        // produces the identical result to Path.Combine for this non-rooted input.
        string joined = Path.Join("a", "b", "c");
        Assert.Equal(
            "math.sqrt(16.0) = 4.0" + NL +
            $"path.join: {joined}" + NL +
            "caught: Required environment variable 'GROB_STDLIB_SMOKE_UNSET' is not set" + NL +
            "guid roundtrip: true" + NL +
            "name   value\nalpha      1\nbeta       2" + NL,
            stdout);
        // log.info/log.error go to stderr (Sprint 8 Increment C); stdlib.grob is not
        // exit-0-with-empty-stderr like the prior four smoke scripts — the contract is
        // stdout, stderr and exit code, per D-337.
        Assert.Equal(
            "stdlib smoke: info line" + NL +
            "stdlib smoke: error line" + NL,
            stderr);
        Assert.Equal(0, exitCode);
    }
}
