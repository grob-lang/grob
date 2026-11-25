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

    // ---- campaign ---------------------------------------------------------------------------

    private string CampaignDirectory => Path.Join(_directory, "campaign");

    /// <summary>Runs each worker in process, through the CLI's own <c>campaign-worker</c> command.</summary>
    private sealed class CliLauncher(string root, TextWriter output) : Campaign.IWorkerLauncher {
        public int Run(string directory, int shard) =>
            Cli.Run(["campaign-worker", "--out", directory, "--shard", shard.ToString(System.Globalization.CultureInfo.InvariantCulture), "--root", root],
                output, output, root);
    }

    private sealed class NoRunner : Campaign.IGrobRunner {
        public Campaign.GrobOutput Run(string scriptPath) => new(0, "", false);
    }

    private int RunCampaign(List<string>? grobPaths, params string[] args) {
        var services = new Campaign.CampaignServices(
            root => new CliLauncher(root, TextWriter.Null),
            path => { grobPaths?.Add(path); return new NoRunner(); });
        return Cli.Run(args, _out, _error, _directory, new Oracle(OracleBudget.Ci), seeds => seeds, services);
    }

    [Fact]
    public void Campaign_SmallRun_RunsEveryProgramThroughItsWorkersAndWritesTheReport() {
        MakeRepository("");

        int exit = RunCampaign(null, "campaign", "--out", CampaignDirectory, "--programs", "6", "--seed", "3", "--workers", "2", "--root", _directory);

        var directory = new Campaign.CampaignDirectory(CampaignDirectory);
        List<ProgramResult> results = [.. Enumerable.Range(0, 2).SelectMany(directory.ReadResults)];
        Assert.Equal(6, results.Count);
        Assert.Equal(results.Any(r => r.Outcome == OutcomeKind.Finding) ? 1 : 0, exit);
        Assert.True(File.Exists(directory.ReportPath));
        Assert.Contains("6 programs", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Campaign_WithGrob_GivesTheRunnerItsFullPath() {
        MakeRepository("");
        Write("grob.exe", "");
        List<string> paths = [];

        RunCampaign(paths, "campaign", "--out", CampaignDirectory, "--programs", "2", "--seed", "3", "--root", _directory, "--grob", "grob.exe");

        Assert.Equal([Path.Join(_directory, "grob.exe")], paths);
    }

    [Fact]
    public void Campaign_GrobThatDoesNotExist_IsAUsageErrorAndRunsNothing() {
        MakeRepository("");

        int exit = RunCampaign(null, "campaign", "--out", CampaignDirectory, "--programs", "2", "--seed", "3", "--root", _directory, "--grob", "absent.exe");

        Assert.Equal(3, exit);
        Assert.Equal($"error: cannot find {Path.Join(_directory, "absent.exe")}\n", _error.ToString().ReplaceLineEndings("\n"));
        Assert.False(Directory.Exists(CampaignDirectory));
    }

    /// <summary>A CLI that exists but cannot be started, as a file without execute permission is.</summary>
    private sealed class UnstartableRunner : Campaign.IGrobRunner {
        public Campaign.GrobOutput Run(string scriptPath) => throw new System.ComponentModel.Win32Exception(13, "Permission denied");
    }

    // The black-box check runs as the report is written, over the stored records. A finished
    // campaign given one record and resumed reaches it at once, whatever its programs found.
    [Fact]
    public void Campaign_GrobThatCannotStart_IsAnErrorThatKeepsTheResultsForAResume() {
        MakeRepository("");
        Write("grob.exe", "");
        string[] args = ["campaign", "--out", CampaignDirectory, "--programs", "2", "--seed", "3", "--workers", "1", "--root", _directory];
        RunCampaign(null, args);
        var directory = new Campaign.CampaignDirectory(CampaignDirectory);
        File.Delete(directory.ReportPath);
        new Findings.FindingStore(directory.FindingsPath).Add(new Findings.FindingRecord("p", "Run|Exception|boom", "boom", null, "x()\n", "x()\n", false));
        var unstartable = new Campaign.CampaignServices(root => new CliLauncher(root, TextWriter.Null), _ => new UnstartableRunner());

        int exit = Cli.Run([.. args, "--grob", "grob.exe"], _out, _error, _directory, new Oracle(OracleBudget.Ci), seeds => seeds, unstartable);

        Assert.Equal(3, exit);
        Assert.Equal("error: Permission denied\n", _error.ToString().ReplaceLineEndings("\n"));
        Assert.Equal(2, directory.ReadResults(0).Count);
        Assert.False(File.Exists(directory.ReportPath));

        int resumed = RunCampaign(null, [.. args, "--grob", "grob.exe"]);

        Assert.Equal(1, resumed);
        Assert.Equal(2, directory.ReadResults(0).Count);
        Assert.True(File.Exists(directory.ReportPath));
    }

    private sealed class FaultyRunner : Campaign.IGrobRunner {
        public Campaign.GrobOutput Run(string scriptPath) => throw new InvalidOperationException("a harness bug");
    }

    [Fact]
    public void Campaign_RunnerThatFaults_IsNotReportedAsACommandError() {
        MakeRepository("");
        Write("grob.exe", "");
        string[] args = ["campaign", "--out", CampaignDirectory, "--programs", "2", "--seed", "3", "--workers", "1", "--root", _directory];
        RunCampaign(null, args);
        new Findings.FindingStore(new Campaign.CampaignDirectory(CampaignDirectory).FindingsPath)
            .Add(new Findings.FindingRecord("p", "Run|Exception|boom", "boom", null, "x()\n", "x()\n", false));
        var faulty = new Campaign.CampaignServices(root => new CliLauncher(root, TextWriter.Null), _ => new FaultyRunner());

        var thrown = Assert.Throws<AggregateException>(() =>
            Cli.Run([.. args, "--grob", "grob.exe"], _out, _error, _directory, new Oracle(OracleBudget.Ci), seeds => seeds, faulty));

        Assert.IsType<InvalidOperationException>(Assert.Single(thrown.InnerExceptions));
        Assert.Empty(_error.ToString());
    }

    [Fact]
    public void Campaign_NoRootGiven_FindsTheRepositoryFromTheWorkingDirectory() {
        MakeRepository("");

        int exit = RunCampaign(null, "campaign", "--out", CampaignDirectory, "--programs", "2", "--seed", "3");

        Assert.InRange(exit, 0, 1);
        Assert.Equal(2, new Campaign.CampaignDirectory(CampaignDirectory).ReadPlan().Count);
    }

    [Fact]
    public void Campaign_RelativeRoot_IsResolvedAgainstTheWorkingDirectory() {
        MakeRepository("");
        List<string> roots = [];
        var services = new Campaign.CampaignServices(
            root => { roots.Add(root); return new CliLauncher(root, TextWriter.Null); },
            _ => new NoRunner());

        Cli.Run(["campaign", "--out", CampaignDirectory, "--programs", "2", "--seed", "3", "--root", "."],
            _out, _error, _directory, new Oracle(OracleBudget.Ci), seeds => seeds, services);

        Assert.Equal([Path.GetFullPath(_directory)], roots);
    }

    [Fact]
    public void CampaignWorker_DirectoryWithNoCampaign_IsAUsageError() {
        int exit = RunCampaign(null, "campaign-worker", "--out", CampaignDirectory, "--shard", "0", "--root", _directory);

        Assert.Equal(3, exit);
        Assert.Equal($"error: {CampaignDirectory} holds no campaign\n", _error.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public void CampaignWorker_ShardOutsideTheCampaign_IsAUsageError() {
        new Campaign.CampaignDirectory(CampaignDirectory).WriteSettings(new Campaign.CampaignSettings(3, 2, 2));

        int exit = RunCampaign(null, "campaign-worker", "--out", CampaignDirectory, "--shard", "2", "--root", _directory);

        Assert.Equal(3, exit);
        Assert.Equal("error: shard 2 is outside a campaign of 2 workers\n", _error.ToString().ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("campaign")]
    [InlineData("campaign", "--out")]
    [InlineData("campaign", "--out", "d", "--seed", "1")]
    [InlineData("campaign", "--out", "d", "--programs", "4")]
    [InlineData("campaign", "--programs", "4", "--seed", "1")]
    [InlineData("campaign", "--out", "d", "--programs", "x", "--seed", "1")]
    [InlineData("campaign", "--out", "d", "--programs", "0", "--seed", "1")]
    [InlineData("campaign", "--out", "d", "--programs", "4", "--seed", "-1")]
    [InlineData("campaign", "--out", "d", "--programs", "4", "--seed", "1", "--workers", "0")]
    [InlineData("campaign", "--out", "d", "--programs", "4", "--seed", "1", "--bogus", "1")]
    [InlineData("campaign", "--out", "d", "--out", "e", "--programs", "4", "--seed", "1")]
    [InlineData("campaign-worker", "--out", "d")]
    [InlineData("campaign-worker", "--shard", "0")]
    [InlineData("campaign-worker", "--out", "d", "--shard", "-1")]
    [InlineData("campaign-worker", "--out", "d", "--shard", "0", "--seed", "1")]
    public void Campaign_BadArguments_PrintsUsageAndExitsThree(params string[] args) {
        int exit = RunCampaign(null, args);

        Assert.Equal(3, exit);
        Assert.StartsWith("usage: grob-torture", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Corpus_ByDefault_RunsASampleOfMutantsBesideTheSeeds() {
        MakeRepository("");
        for (int i = 0; i < 60; i++) {
            Write(Path.Join("tests", "fixtures", $"seed{i}.grob"), $"fn f{i}(a: int): int {{\n    return a + {i}\n}}\nx := f{i}(1)\nprint(x)\n");
        }

        int exit = Cli.Run(["corpus", _directory], _out, _error, _directory);

        Assert.Equal(1, exit);
        string header = _out.ToString().ReplaceLineEndings("\n").Split('\n')[0];
        int programs = int.Parse(header[..header.IndexOf(' ', StringComparison.Ordinal)], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(programs > 62, $"expected the 62 seeds and some mutants, got: {header}");
    }
}
