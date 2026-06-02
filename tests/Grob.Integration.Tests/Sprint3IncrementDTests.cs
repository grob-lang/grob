using System.Text;
using Grob.Cli;
using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 3 Increment D integration tests: nullable runtime —
/// <c>??</c> (eager nil-coalesce) and <c>E0101</c> compile-time guard.
/// Tests drive through <see cref="RunCommand"/> with captured stdout/stderr writers.
/// </summary>
/// <remarks>
/// <c>?.</c> end-to-end is not exercisable until Sprint 5 (struct fields).
/// These tests cover <c>??</c> — the primary runtime feature of this increment.
/// </remarks>
public sealed class Sprint3IncrementDTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) {
        if (Path.IsPathRooted(name))
            throw new ArgumentException($"Fixture name must be a relative path, got: {name}", nameof(name));
        return Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-3d", name);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    // -----------------------------------------------------------------------
    // nil ?? fallback — literal nil on the left
    // -----------------------------------------------------------------------

    [Fact]
    public void NilCoalesce_NilLiteral_PrintsFallback() {
        (string stdout, string stderr, int exitCode) = RunFile("nil-coalesce-nil.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal($"default{NL}", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void NilCoalesce_NonNilLiteral_PrintsLeft() {
        (string stdout, string stderr, int exitCode) = RunFile("nil-coalesce-nonnull.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal($"hello{NL}", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // nil ?? fallback — nullable variable
    // -----------------------------------------------------------------------

    [Fact]
    public void NilCoalesce_NullableIntVar_Nil_PrintsFallback() {
        (string stdout, string stderr, int exitCode) = RunFile("nullable-int-coalesce-nil.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal($"0{NL}", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void NilCoalesce_NullableIntVar_NonNil_PrintsValue() {
        (string stdout, string stderr, int exitCode) = RunFile("nullable-int-coalesce-nonnull.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal($"42{NL}", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // E0101 — compile-time guard: '.' on nullable without '?.' or '??'
    // -----------------------------------------------------------------------

    [Fact]
    public void PlainDotOnNullable_WritesE0101ToStderrAndExitsNonZero() {
        (string stdout, string stderr, int exitCode) = RunFile("nil-dot-error.grob");

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0101", stderr);
    }
}
