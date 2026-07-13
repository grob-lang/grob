using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 8 Increment B integration test: the completed <c>math</c> module, <c>path</c>,
/// and <c>strings.join</c> end to end through the full pipeline (lex -> parse ->
/// type-check -> compile -> VM, stdlib plugins auto-registered at startup) — including
/// the <c>Grob.Cli</c> composition-root wiring this increment added
/// (<c>SystemRandomSource</c>, <c>PluginRegistration.RegisterAll</c>'s new signature).
/// </summary>
public sealed class Sprint8IncrementBTests {
    private static string FixturePath(string name) {
        if (Path.IsPathRooted(name))
            throw new ArgumentException($"Fixture name must be a relative path, got: {name}", nameof(name));
        return Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-8b", name);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    private static string NL => Environment.NewLine;

    [Fact]
    public void StdlibPureVertical_RunFile_PrintsExpectedOutputAndExitsZero() {
        (string stdout, string stderr, int exitCode) = RunFile("stdlib-pure-vertical.grob");

        // The fixture's path.join/path.normalise inputs are relative (not drive-letter
        // absolute) precisely so the expected output can be computed via Path.Combine
        // here, matching whatever separator the platform this test runs on actually
        // uses (ADR-0007 — path is platform-aware at runtime; a hardcoded Windows
        // literal only ever holds on the windows-latest CI leg).
        string joined = Path.Combine("Reports", "2026", "Q1", "summary.xlsx");
        string normalised = Path.Combine("a", "c");
        string expected =
            $"1024.0{NL}" +
            $"1.0{NL}" +
            $"1.0{NL}" +
            $"180.0{NL}" +
            $"true{NL}" +
            $"caught domain error: math.log: domain error — argument 0 is not positive{NL}" +
            $"{joined}{NL}" +
            $".xlsx{NL}" +
            $"{normalised}{NL}" +
            $"Alice, Bob, Charlie{NL}" +
            $"done{NL}";

        Assert.Equal(0, exitCode);
        Assert.Equal(expected, stdout);
        Assert.Equal(string.Empty, stderr);
    }
}
