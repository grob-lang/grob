using Grob.Torture.Campaign;
using Grob.Torture.Findings;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The supervisor builds or resumes a campaign's plan, keeps a worker running on each shard, turns
/// a worker's death into a finding for the program its journal names, and writes the report with
/// the black-box check of every distinct reproducer (gate report §4.3, §4.4).
/// </summary>
public sealed class CampaignSupervisorTests : IDisposable {
    private const ulong Seed = 5;
    private const int StackOverflow = unchecked((int)0xC00000FD);

    private readonly string _root = Path.Join(Path.GetTempPath(), $"grob-supervisor-{Guid.NewGuid():N}");
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly List<Seed> _corpus = [
        new("one@1", SeedKind.Fixture, "one.grob", "x := 1 + 2\ny := x * 3\nprint(y)\n"),
        new("two@2", SeedKind.Fixture, "two.grob", "s := \"ab\"\nprint(s.upper())\nn := 4 - 1\nprint(n)\n"),
    ];

    public void Dispose() {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private CampaignDirectory Directory_ => new(_root);

    private string WriteProgram(PlannedProgram program) =>
        CampaignPlan.Write(program, Seed, _corpus.ToDictionary(s => s.Id, StringComparer.Ordinal));

    private CampaignWorker InProcess(string directory, int shard) =>
        new(new CampaignDirectory(directory), shard, WriteProgram, OracleBudget.Ci, OracleBudget.Ci);

    /// <summary>Runs each launch through <paramref name="launch"/>, counting launches by shard.</summary>
    private sealed class Launcher(Func<string, int, int, int> launch) : IWorkerLauncher {
        private readonly Dictionary<int, int> _launches = [];

        public int Launches(int shard) {
            lock (_launches) return _launches.GetValueOrDefault(shard);
        }

        public int Run(string directory, int shard) {
            int attempt;
            lock (_launches) attempt = _launches[shard] = _launches.GetValueOrDefault(shard) + 1;
            return launch(directory, shard, attempt);
        }
    }

    private sealed class Runner(Func<string, GrobOutput> run) : IGrobRunner {
        public List<string> Scripts { get; } = [];

        public GrobOutput Run(string scriptPath) {
            lock (Scripts) Scripts.Add(File.ReadAllText(scriptPath));
            return run(scriptPath);
        }
    }

    private CampaignSupervisor Supervisor(IWorkerLauncher launcher, IGrobRunner? grob = null, Func<IReadOnlyList<Seed>>? corpus = null) =>
        new(launcher, corpus ?? (() => _corpus), grob, known: null, _out, _error);

    private IReadOnlyList<ProgramResult> AllResults(int workers) =>
        [.. Enumerable.Range(0, workers).SelectMany(Directory_.ReadResults)];

    // What a worker's journal holds when it dies, written as the worker would leave it.
    private void LeaveJournal(int shard, string programId, string state) {
        string path = Directory_.JournalPath(shard);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"{programId}\t{state}\n");
    }

    private PlannedProgram FirstOfShard(int shard, int workers) =>
        Directory_.ReadPlan().First(p => CampaignPlan.ShardOf(p.Id, workers) == shard);

    [Fact]
    public void Run_EveryShard_RunsEveryPlannedProgramOnceAndTheReportIsWritten() {
        var launcher = new Launcher((d, s, _) => InProcess(d, s).Run());

        int exit = Supervisor(launcher).Run(_root, new CampaignSettings(Seed, 12, 2));

        IReadOnlyList<ProgramResult> results = AllResults(2);
        Assert.Equal(Directory_.ReadPlan().Select(p => p.Id).Order(), results.Select(r => r.ProgramId).Order());
        Assert.Equal(results.Any(r => r.Outcome == OutcomeKind.Finding) ? 1 : 0, exit);
        Assert.True(File.Exists(Directory_.ReportPath));
        Assert.Contains("12 programs", _out.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, launcher.Launches(0));
        Assert.Equal(1, launcher.Launches(1));
    }

    [Fact]
    public void Run_ACampaignStartedBefore_ResumesItsPlanWithoutReadingTheCorpus() {
        var settings = new CampaignSettings(Seed, 6, 1);
        Supervisor(new Launcher((_, _, _) => 0)).Run(_root, settings);
        IReadOnlyList<PlannedProgram> plan = Directory_.ReadPlan();

        int exit = Supervisor(new Launcher((d, s, _) => InProcess(d, s).Run()), corpus: () => throw new InvalidOperationException("read")).Run(_root, settings);

        Assert.Equal(plan, Directory_.ReadPlan());
        Assert.Equal(6, AllResults(1).Count);
        Assert.InRange(exit, 0, 1);
    }

    [Fact]
    public void Run_ADirectoryHoldingAnotherCampaign_IsAUsageErrorAndRunsNothing() {
        Directory_.WriteSettings(new CampaignSettings(Seed, 6, 1));
        var launcher = new Launcher((_, _, _) => 0);

        int exit = Supervisor(launcher).Run(_root, new CampaignSettings(Seed + 1, 6, 1));

        Assert.Equal(3, exit);
        Assert.Equal(0, launcher.Launches(0));
        Assert.Contains("holds another campaign", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_AWorkerThatDiesRunningAProgram_MakesAProcessDeathFindingAndIsRestartedPastIt() {
        string? died = null;
        var launcher = new Launcher((d, s, attempt) => {
            if (attempt > 1) return InProcess(d, s).Run();
            died = FirstOfShard(0, 1).Id;
            LeaveJournal(0, died, "Parse");
            return StackOverflow;
        });

        int exit = Supervisor(launcher).Run(_root, new CampaignSettings(Seed, 6, 1));

        Assert.Equal(1, exit);
        Assert.Equal(2, launcher.Launches(0));
        ProgramResult death = Assert.Single(AllResults(1), r => r.ProgramId == died);
        Finding finding = Assert.Single(death.Findings);
        Assert.Equal("Parse|ProcessDeath|0xC00000FD", finding.Signature.Key);
        Assert.Equal(6, AllResults(1).Count);
        FindingRecord record = Assert.Single(new FindingStore(Directory_.FindingsPath).All(), r => r.ProgramId == died);
        Assert.Equal(record.Original, record.Reproducer);
        Assert.Contains($"{died}\tthe worker died running it, exit 0xC00000FD", Directory_.ReadUnminimised());
    }

    [Fact]
    public void Run_AWorkerThatDiesWhileMinimising_StoresTheUnminimisedRecordAndIsRestarted() {
        PlannedProgram? victim = null;
        var finding = new Finding(new Signature(PipelinePhase.Run, FindingClass.Exception, "GrobInternalException: boom"), "boom");
        var launcher = new Launcher((d, s, attempt) => {
            if (attempt > 1) return InProcess(d, s).Run();
            victim = FirstOfShard(0, 1);
            Directory_.AppendResult(0, new ProgramResult(victim.Id, OutcomeKind.Finding, [finding]));
            LeaveJournal(0, victim.Id, Journal.Minimising);
            return StackOverflow;
        });

        Supervisor(launcher).Run(_root, new CampaignSettings(Seed, 6, 1));

        Assert.Equal(2, launcher.Launches(0));
        Assert.Single(AllResults(1), r => r.ProgramId == victim!.Id);
        Assert.Contains($"{victim!.Id}\tthe worker died while minimising it, exit 0xC00000FD", Directory_.ReadUnminimised());
        FindingRecord record = Assert.Single(new FindingStore(Directory_.FindingsPath).Records(finding.Signature.Key));
        Assert.Equal(WriteProgram(victim), record.Original);
        Assert.Equal(record.Original, record.Reproducer);
    }

    [Fact]
    public void Run_AWorkerThatDiesAfterRunningAProgram_StoresItsFindingsUnminimisedAndRecordsNoDeath() {
        PlannedProgram? victim = null;
        var finding = new Finding(new Signature(PipelinePhase.Run, FindingClass.Exception, "GrobInternalException: boom"), "boom");
        var launcher = new Launcher((d, s, attempt) => {
            if (attempt > 1) return InProcess(d, s).Run();
            victim = FirstOfShard(0, 1);
            Directory_.AppendResult(0, new ProgramResult(victim.Id, OutcomeKind.Finding, [finding]));
            LeaveJournal(0, victim.Id, "Run");
            return StackOverflow;
        });

        int exit = Supervisor(launcher).Run(_root, new CampaignSettings(Seed, 6, 1));

        Assert.Equal(1, exit);
        ProgramResult only = Assert.Single(AllResults(1), r => r.ProgramId == victim!.Id);
        Assert.Equal(finding, Assert.Single(only.Findings));
        FindingRecord record = Assert.Single(new FindingStore(Directory_.FindingsPath).Records(finding.Signature.Key));
        Assert.Equal(WriteProgram(victim!), record.Original);
        Assert.Contains($"{victim!.Id}\tthe worker died after running it, exit 0xC00000FD", Directory_.ReadUnminimised());
    }

    [Fact]
    public void Run_AJournalAnEarlierDeathNamed_IsNotReadForALaterDeath() {
        var launcher = new Launcher((_, _, attempt) => {
            if (attempt == 1) LeaveJournal(0, FirstOfShard(0, 1).Id, "Parse");
            return StackOverflow;
        });

        int exit = Supervisor(launcher).Run(_root, new CampaignSettings(Seed, 6, 1));

        Assert.Equal(1, exit);
        Assert.Equal(1 + CampaignSupervisor.MaxSilentDeaths, launcher.Launches(0));
        Assert.Single(AllResults(1), r => r.Findings.Any(f => f.Signature.Class == FindingClass.ProcessDeath));
    }

    [Fact]
    public void Run_AFindingResultWithNoRecord_GetsAnUnminimisedRecordAndTheRunFails() {
        PlannedProgram? orphan = null;
        var finding = new Finding(new Signature(PipelinePhase.Run, FindingClass.Exception, "GrobInternalException: boom"), "boom");
        var launcher = new Launcher((_, _, _) => {
            orphan = FirstOfShard(0, 1);
            Directory_.AppendResult(0, new ProgramResult(orphan.Id, OutcomeKind.Finding, [finding]));
            return 0;
        });

        int exit = Supervisor(launcher).Run(_root, new CampaignSettings(Seed, 6, 1));

        Assert.Equal(1, exit);
        FindingRecord record = Assert.Single(new FindingStore(Directory_.FindingsPath).All());
        Assert.Equal(orphan!.Id, record.ProgramId);
        Assert.Equal(WriteProgram(orphan), record.Reproducer);
        Assert.Contains($"{orphan.Id}\tits record was missing when the report was written", Directory_.ReadUnminimised());
    }

    [Fact]
    public void Run_AWorkerThatAsksToBeRestarted_IsRestartedWithNoDeathRecorded() {
        var launcher = new Launcher((d, s, attempt) => attempt == 1 ? CampaignWorker.RestartExitCode : InProcess(d, s).Run());

        Supervisor(launcher).Run(_root, new CampaignSettings(Seed, 6, 1));

        Assert.Equal(2, launcher.Launches(0));
        Assert.Empty(Directory_.ReadUnminimised());
        Assert.DoesNotContain(AllResults(1), r => r.Findings.Any(f => f.Signature.Class == FindingClass.ProcessDeath));
    }

    [Fact]
    public void Run_AWorkerThatDiesWithoutNamingAProgram_IsAbandonedAfterSoManyTries() {
        var launcher = new Launcher((d, s, _) => s == 0 ? 1 : InProcess(d, s).Run());

        int exit = Supervisor(launcher).Run(_root, new CampaignSettings(Seed, 8, 2));

        Assert.Equal(1, exit);
        Assert.Equal(CampaignSupervisor.MaxSilentDeaths, launcher.Launches(0));
        Assert.Equal(1, launcher.Launches(1));
        Assert.Contains("shard 0: the worker died 3 times without naming a program; abandoned", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Run_WithTheRealCli_ChecksEachDistinctReproducerOnceAndReportsTheVerdict() {
        var store = new FindingStore(Directory_.FindingsPath);
        store.Add(new FindingRecord("a", "Run|Exception|X", "x", null, "o\n", "repro one\n", false));
        store.Add(new FindingRecord("b", "Run|Exception|X", "x", null, "o\n", "repro one\n", false));
        store.Add(new FindingRecord("c", "Run|Exception|X", "x", null, "o\n", "repro two\n", false));
        var runner = new Runner(path => File.ReadAllText(path).Contains("one", StringComparison.Ordinal)
            ? new GrobOutput(1, "error[E9001]: internal compiler error — please report\n", false)
            : new GrobOutput(-532462766, "Unhandled exception. System.Exception\n   at Grob.Vm.VirtualMachine.Run(Chunk chunk)\n", false));

        int exit = Supervisor(new Launcher((_, _, _) => 0), runner).Run(_root, new CampaignSettings(Seed, 4, 1));

        Assert.Equal(1, exit);
        Assert.Equal(["repro one\n", "repro two\n"], runner.Scripts.Order(StringComparer.Ordinal));
        string report = File.ReadAllText(Directory_.ReportPath);
        Assert.Contains("| Run\\|Exception\\|X | Reported | 1 | 2 |", report, StringComparison.Ordinal);
        Assert.Contains("| Run\\|Exception\\|X | StackTrace | -532462766 | 1 |", report, StringComparison.Ordinal);
    }
}
