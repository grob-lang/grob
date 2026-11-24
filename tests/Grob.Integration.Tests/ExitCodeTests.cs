using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// The CLI's exit codes are the documented set (requirements §8; the adversarial strategy's §1;
/// R-64, D-451 Decision 5): <c>0</c> for a clean run, <c>1</c> for a Grob runtime error, <c>2</c>
/// for a compile-time diagnostic, <c>3</c> for a usage error and <c>n</c> for a script's own
/// <c>exit(n)</c>. Each test drives <see cref="CommandLineRunner"/>, the path <c>Program.cs</c> takes.
/// </summary>
public sealed class ExitCodeTests {
    private static (string Stdout, string Stderr, int ExitCode) Run(string[] args) {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = new CommandLineRunner(TextReader.Null, stdout, stderr).Run(args);
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunScript(string source) {
        string path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            return Run(["run", path]);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void CleanRun_ExitsZero() {
        (_, string stderr, int exitCode) = RunScript("print(\"ok\")\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void RuntimeError_ExitsOne() {
        (_, string stderr, int exitCode) = RunScript("a := 1\nb := 0\nprint(a / b)\n");

        Assert.Equal(1, exitCode);
        Assert.StartsWith("error[E5002]", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void TypeError_ExitsTwo() {
        (_, string stderr, int exitCode) = RunScript("x: int := \"text\"\n");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("error[E0001]", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void SyntaxError_ExitsTwo() {
        (_, string stderr, int exitCode) = RunScript("x := (\n");

        Assert.Equal(2, exitCode);
        Assert.StartsWith("error[E2001]", stderr, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(255)]
    public void ScriptExit_ExitsWithTheScriptsCode(int code) {
        (_, string stderr, int exitCode) = RunScript($"exit({code})\n");

        Assert.Equal(code, exitCode);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void UnknownCommand_ExitsThree() {
        (_, _, int exitCode) = Run(["frobnicate"]);

        Assert.Equal(3, exitCode);
    }

    [Fact]
    public void RunWithoutAPath_ExitsThree() {
        (_, _, int exitCode) = Run(["run"]);

        Assert.Equal(3, exitCode);
    }

    // An empty path is no path: File.ReadAllText refuses it with an ArgumentException, which must
    // not escape as a crash.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RunWithAnEmptyPath_ExitsThreeAsAMissingPath(string path) {
        (_, string stderr, int exitCode) = Run(["run", path]);

        Assert.Equal(3, exitCode);
        Assert.StartsWith($"error: missing file path{Environment.NewLine}", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void RunOfAPathTheFileSystemRefuses_ExitsThree() {
        (_, string stderr, int exitCode) = Run(["run", "bad\0name.grob"]);

        Assert.Equal(3, exitCode);
        Assert.StartsWith("error: invalid file path: ", stderr, StringComparison.Ordinal);
    }

    [Fact]
    public void RunOfAFileThatDoesNotExist_ExitsThree() {
        string missing = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");

        (_, string stderr, int exitCode) = Run(["run", missing]);

        Assert.Equal(3, exitCode);
        Assert.Equal($"error: file not found: {missing}{Environment.NewLine}", stderr);
    }

    [Fact]
    public void RunOfADirectoryInPlaceOfAFileThatDoesNotExist_ExitsThree() {
        string missing = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "nested.grob");

        (_, _, int exitCode) = Run(["run", missing]);

        Assert.Equal(3, exitCode);
    }

    [Fact]
    public void RunOfAFileTheCliCannotRead_ExitsThree() {
        // A directory named as the script: reading it raises UnauthorizedAccessException on
        // Windows and an IOException on Linux, and both are "the path cannot be used".
        string directory = Directory.CreateTempSubdirectory("grob-exit-code-").FullName;
        try {
            (_, string stderr, int exitCode) = Run(["run", directory]);

            Assert.Equal(3, exitCode);
            Assert.StartsWith("error: ", stderr, StringComparison.Ordinal);
        } finally {
            Directory.Delete(directory);
        }
    }
}
