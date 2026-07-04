using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 6 Increment E integration tests — the sprint-close smoke script that
/// exercises the complete §7 language surface: type declarations with field
/// defaults, named and nested construction, field access and assignment, a
/// recursive type built and traversed, a <c>#{ }</c> projection through
/// <c>.select()</c>, and a closure escaping through a struct field (D-325).
/// </summary>
public sealed class Sprint6IncrementETests {
    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) =>
        Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-6e", name);

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    [Fact]
    public void TypesSmoke_RunsAndProducesExpectedOutput() {
        (string stdout, string stderr, int exitCode) = RunFile("types.grob");

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(
            $"example.com:80{NL}example.com:8080{NL}Alice lives in London{NL}Paris{NL}6{NL}Bob - Berlin{NL}Carol - Madrid{NL}42{NL}",
            stdout);
        Assert.Equal(0, exitCode);
    }
}
