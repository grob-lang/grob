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

    private int Run(params string[] args) => Cli.Run(args, _out, _error, _directory, new Oracle(OracleBudget.Ci), seeds => seeds);

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

        int exit = Run("check", path);

        Assert.Equal(1, exit);
        Assert.Contains("    at Grob.Vm.", _out.ToString(), StringComparison.Ordinal);
    }

    // The oracle's hook stalls the checker, so the first run passes its watchdog.
    private static Oracle StallingOracle(ManualResetEventSlim release) =>
        new(new OracleBudget(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500)), phaseHook: p => {
            if (p == PipelinePhase.Check) release.Wait();
        });

    [Fact]
    public void Check_RunPastItsWatchdog_StopsBeforeTheNextFile() {
        string first = Write("first.grob", "print(1)\n");
        string second = Write("second.grob", "print(2)\n");
        using var release = new ManualResetEventSlim();
        new Oracle(OracleBudget.Ci).Run("print(0)\n");

        try {
            int exit = Cli.Run(["check", first, second], _out, _error, _directory, StallingOracle(release));

            Assert.Equal(1, exit);
            Assert.Equal(
                $"{first}: Finding\n  Check|Timeout|watchdog  the run did not end within 550 ms\n" +
                "stopped: the last run did not end, and its worker cannot be stopped\n",
                _out.ToString().ReplaceLineEndings("\n"));
        } finally {
            release.Set();
        }
    }

    [Fact]
    public void Corpus_RunPastItsWatchdog_SaysTheSliceStopped() {
        MakeRepository("");
        using var release = new ManualResetEventSlim();
        new Oracle(OracleBudget.Ci).Run("print(0)\n");

        try {
            int exit = Cli.Run(["corpus", _directory], _out, _error, _directory, StallingOracle(release), seeds => seeds);

            Assert.Equal(1, exit);
            string text = _out.ToString().ReplaceLineEndings("\n");
            Assert.StartsWith("1 programs: Finding 1\n", text, StringComparison.Ordinal);
            Assert.Contains("stopped: the last run did not end, and its worker cannot be stopped\n", text, StringComparison.Ordinal);
        } finally {
            release.Set();
        }
    }

    // A known timeout still stops the slice, and the programs after it are neither run nor judged,
    // so the run is not a success even though every finding it made is known.
    [Fact]
    public void Corpus_RunStoppedAtAKnownTimeout_ExitsOne() {
        string cleanProgram = $"tests/fixtures/clean.grob@{SeedCorpus.Fnv1a64("print(1)\n"):x16}";
        MakeRepository($$"""{ "row": "R-73", "signature": "Check|Timeout|watchdog", "programs": ["{{cleanProgram}}"] }""");
        using var release = new ManualResetEventSlim();
        new Oracle(OracleBudget.Ci).Run("print(0)\n");

        try {
            int exit = Cli.Run(["corpus", _directory], _out, _error, _directory, StallingOracle(release));

            Assert.Equal(1, exit);
            string text = _out.ToString().ReplaceLineEndings("\n");
            Assert.Contains($"known    R-73  {cleanProgram}  Check|Timeout|watchdog\n", text, StringComparison.Ordinal);
            Assert.Contains("stopped: the last run did not end, and its worker cannot be stopped\n", text, StringComparison.Ordinal);
        } finally {
            release.Set();
        }
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
    [InlineData("corpus", "a", "b")]
    public void Run_BadArguments_PrintsUsageAndExitsThree(params string[] args) {
        int exit = Run(args);

        Assert.Equal(3, exit);
        Assert.StartsWith("usage: grob-torture check <file>... | corpus [<root>]", _error.ToString(), StringComparison.Ordinal);
    }

    private void MakeRepository(string knownEntries) {
        Write("Grob.slnx", "<Solution />");
        Directory.CreateDirectory(Path.Join(_directory, "tests", "fixtures"));
        Directory.CreateDirectory(Path.Join(_directory, "tests", "Grob.Torture.Tests"));
        Directory.CreateDirectory(Path.Join(_directory, "docs", "design"));
        Write(Path.Join("tests", "fixtures", "clean.grob"), "print(1)\n");
        Write(Path.Join("tests", "fixtures", "range.grob"), "exit(300)\n");
        Write(Path.Join("docs", "design", "grob-open-questions.md"),
            "### Open items\n\n| # | Item | Raised by | Owner | Done when |\n|---|---|---|---|---|\n| R-73 | **x** | D-451 | `unowned` | y |\n\n### Closed items\n");
        Write(Path.Join("tests", "Grob.Torture.Tests", "known-findings.json"), $$"""{ "entries": [{{knownEntries}}] }""");
    }

    private static string RangeProgram => $"tests/fixtures/range.grob@{SeedCorpus.Fnv1a64("exit(300)\n"):x16}";

    [Fact]
    public void Corpus_UntriagedFinding_IsListedAsNewAndExitsOne() {
        MakeRepository("");

        int exit = Run("corpus", _directory);

        Assert.Equal(1, exit);
        string text = _out.ToString().ReplaceLineEndings("\n");
        Assert.StartsWith("2 programs: Clean 1, Finding 1\n", text, StringComparison.Ordinal);
        Assert.Contains($"new      {RangeProgram}  Run|ExitRange|above-255  exit(300)\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Corpus_EveryFindingKnown_ListsThemAndExitsZero() {
        MakeRepository($$"""{ "row": "R-73", "signature": "Run|ExitRange|above-255", "programs": ["{{RangeProgram}}"] }""");

        int exit = Run("corpus", _directory);

        Assert.Equal(0, exit);
        Assert.Contains($"known    R-73  {RangeProgram}  Run|ExitRange|above-255\n", _out.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void Corpus_StaleEntryAndClosedRow_AreListedAndExitOne() {
        MakeRepository("""{ "row": "R-12", "signature": "Run|ExitRange|negative", "programs": ["gone@0"] }""");

        int exit = Run("corpus", _directory);

        Assert.Equal(1, exit);
        string text = _out.ToString().ReplaceLineEndings("\n");
        Assert.Contains("stale    R-12 gone@0 Run|ExitRange|negative: not in the corpus\n", text, StringComparison.Ordinal);
        Assert.Contains("problem  known finding row R-12 is not in the register\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Corpus_NoRootGiven_FindsTheRepositoryFromTheWorkingDirectory() {
        MakeRepository($$"""{ "row": "R-73", "signature": "Run|ExitRange|above-255", "programs": ["{{RangeProgram}}"] }""");
        Directory.CreateDirectory(Path.Join(_directory, "sub"));

        int exit = Cli.Run(["corpus"], _out, _error, Path.Join(_directory, "sub"), new Oracle(OracleBudget.Ci), seeds => seeds);

        Assert.Equal(0, exit);
    }

    [Fact]
    public void Corpus_ByDefault_RunsASampleOfMutantsBesideTheSeeds() {
        MakeRepository("");
        for (int i = 0; i < 60; i++) {
            Write(Path.Join("tests", "fixtures", $"seed{i}.grob"), $"fn f{i}(a: int): int {{\n    return a + {i}\n}}\nx := f{i}(1)\nprint(x)\n");
        }

        Cli.Run(["corpus", _directory], _out, _error, _directory);

        string header = _out.ToString().ReplaceLineEndings("\n").Split('\n')[0];
        int programs = int.Parse(header[..header.IndexOf(' ', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(programs > 62, $"expected the 62 seeds and some mutants, got: {header}");
    }
}
