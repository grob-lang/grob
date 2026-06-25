using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 5 Increment F integration tests — the sprint-close smoke script that
/// exercises the complete §6 language surface: recursion, named and default
/// arguments, category-4 closures with per-call independence, a
/// filter/select/sort pipeline with a capturing lambda, and flow-sensitive
/// narrowing.
/// </summary>
public sealed class Sprint5IncrementFTests {
    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) =>
        Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-5f", name);

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    [Fact]
    public void FunctionsSmoke_RunsAndProducesExpectedOutput() {
        (string stdout, string stderr, int exitCode) = RunFile("functions.grob");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal(
            $"120{NL}Hello, World!{NL}Hi, Alice!{NL}a=3 b=2{NL}8{NL}10{NL}value: hello{NL}no value{NL}",
            stdout);
    }
}
