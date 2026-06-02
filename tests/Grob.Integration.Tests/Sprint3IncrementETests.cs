using System.Text;
using Grob.Cli;
using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 3 Increment E integration tests: string interpolation end-to-end.
/// Exercises segment-push + <c>BuildString</c> compilation and VM execution,
/// including nullable-slot E0102 enforcement and <c>??</c>-resolved fallback.
/// </summary>
public sealed class Sprint3IncrementETests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) {
        if (Path.IsPathRooted(name))
            throw new ArgumentException($"Fixture name must be a relative path, got: {name}", nameof(name));
        return Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-3e", name);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    // -----------------------------------------------------------------------
    // Happy path: interpolated string from declared variables
    // -----------------------------------------------------------------------

    [Fact]
    public void InterpolateVariables_PrintsExpectedString() {
        (string stdout, string stderr, int exitCode) = RunFile("interpolate-variables.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal($"Hello, Alice! You are 30 years old.{NL}", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // Nullable fallback: ${user ?? "guest"} resolves to non-nullable string
    // -----------------------------------------------------------------------

    [Fact]
    public void InterpolateNullableFallback_PrintsFallback() {
        (string stdout, string stderr, int exitCode) = RunFile("interpolate-nullable-fallback.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal($"Hello, guest!{NL}", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // E0102: nullable slot without ?? is a compile error
    // -----------------------------------------------------------------------

    [Fact]
    public void InterpolateNullableWithoutCoalesce_EmitsE0102AndExitsNonZero() {
        (string stdout, string stderr, int exitCode) = RunFile("interpolate-nullable-error.grob");

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0102", stderr);
    }
}
