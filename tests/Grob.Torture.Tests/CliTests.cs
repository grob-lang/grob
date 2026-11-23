using Xunit;

namespace Grob.Torture.Tests;

/// <summary>The command-line surface: <c>check</c> runs files through the oracle by hand.</summary>
public sealed class CliTests : IDisposable {
    private readonly string _directory = Directory.CreateTempSubdirectory("grob-torture-cli-").FullName;
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string Write(string name, string source) {
        string path = Path.Join(_directory, name);
        File.WriteAllText(path, source);
        return path;
    }

    private int Run(params string[] args) => Cli.Run(args, _out, _error);

    [Fact]
    public void Check_CleanFile_PrintsOutcomeAndExitsZero() {
        string path = Write("clean.grob", "print(1)\n");

        int exit = Run("check", path);

        Assert.Equal(0, exit);
        Assert.Equal($"{path}: Clean\n", _out.ToString().ReplaceLineEndings("\n"));
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public void Check_FileWithAFinding_PrintsSignatureAndExitsOne() {
        string clean = Write("clean.grob", "print(1)\n");
        string bad = Write("bad.grob", "exit(300)\n");

        int exit = Run("check", clean, bad);

        Assert.Equal(1, exit);
        Assert.Equal(
            $"{clean}: Clean\n{bad}: Finding\n  Run|ExitRange|above-255  exit(300)\n",
            _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Check_FindingWithAFrame_PrintsTheFrame() {
        string path = Write("r60.grob", "xs := [1, 2]\nprint(xs.filter(5))\n");

        Run("check", path);

        Assert.Contains("    at Grob.Vm.", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Check_MissingFile_IsUsageErrorExitThree() {
        string path = Path.Join(_directory, "absent.grob");

        int exit = Run("check", path);

        Assert.Equal(3, exit);
        Assert.StartsWith($"error: cannot read {path}:", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Check_DirectoryForAFile_IsUsageErrorExitThree() {
        int exit = Run("check", _directory);

        Assert.Equal(3, exit);
        Assert.StartsWith($"error: cannot read {_directory}:", _error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData]
    [InlineData("check")]
    [InlineData("frobnicate", "a.grob")]
    public void Run_BadArguments_PrintsUsageAndExitsThree(params string[] args) {
        int exit = Run(args);

        Assert.Equal(3, exit);
        Assert.StartsWith("usage: grob-torture check <file>...", _error.ToString(), StringComparison.Ordinal);
    }
}
