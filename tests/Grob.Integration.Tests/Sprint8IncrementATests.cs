using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 8 Increment A integration tests: the <c>math.pi</c>/<c>math.sqrt</c>
/// proving vertical end to end through the full pipeline (lex -> parse -> type-check
/// -> compile -> VM, stdlib plugins auto-registered at startup), and the two new
/// namespace compile-time diagnostics (D-342).
/// </summary>
public sealed class Sprint8IncrementATests {
    private static string FixturePath(string name) {
        if (Path.IsPathRooted(name))
            throw new ArgumentException($"Fixture name must be a relative path, got: {name}", nameof(name));
        return Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-8a", name);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    private static string NL => Environment.NewLine;

    [Fact]
    public void MathVertical_RunFile_PrintsExpectedOutputAndExitsZero() {
        (string stdout, string stderr, int exitCode) = RunFile("math-vertical.grob");

        string expected =
            $"3.141592653589793{NL}" +
            $"3.0{NL}" +
            $"caught domain error: math.sqrt: domain error — argument -4 is negative{NL}" +
            $"done{NL}";

        Assert.Equal(0, exitCode);
        Assert.Equal(expected, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void MathSqrtNegative_Unhandled_ExitsOneWithTopLevelDiagnostic() {
        (string stdout, string stderr, int exitCode) = RunFile("math-sqrt-unhandled.grob");

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5006", stderr);
        Assert.Contains("math-sqrt-unhandled.grob", stderr);
    }

    [Fact]
    public void NamespaceUsedAsValue_BindingTarget_IsCompileErrorE1004() {
        (string _, string stderr, int exitCode) = RunSource("result := math\nprint(result)\n");

        Assert.Equal(1, exitCode);
        Assert.Contains("E1004", stderr);
    }

    [Fact]
    public void NamespaceUsedAsValue_CallArgument_IsCompileErrorE1004() {
        (string _, string stderr, int exitCode) = RunSource("print(math)\n");

        Assert.Equal(1, exitCode);
        Assert.Contains("E1004", stderr);
    }

    [Fact]
    public void UnknownNamespaceMember_IsCompileErrorE1003() {
        (string _, string stderr, int exitCode) = RunSource("print(math.nope())\n");

        Assert.Equal(1, exitCode);
        Assert.Contains("E1003", stderr);
    }
}
