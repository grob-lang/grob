using Grob.Torture.Campaign;
using Grob.Torture.Findings;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// A campaign worker runs its shard of the plan, keeps a journal of the program and phase it is in,
/// appends one result per program, minimises each finding and stores its record, and asks to be
/// restarted after a run its watchdog abandoned (gate report §4.4).
/// </summary>
public sealed class CampaignWorkerTests : IDisposable {
    // R-60: the checker accepts a non-function argument to filter, and the VM stops.
    private const string Failing = "a := 1\nb := 2\nxs := [1, 2]\nprint(xs.filter(5))\nc := 3\n";

    private readonly string _root = Path.Join(Path.GetTempPath(), $"grob-worker-{Guid.NewGuid():N}");
    private readonly CampaignDirectory _directory;
    private readonly Dictionary<string, string> _sources = new(StringComparer.Ordinal);

    public CampaignWorkerTests() => _directory = new CampaignDirectory(_root);

    public void Dispose() {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    // A plan of the given programs, all in shard 0 of one worker unless stated.
    private void Plan(int workers, params (string Id, string Source)[] programs) {
        foreach ((string id, string source) in programs) _sources[id] = source;
        _directory.WriteSettings(new CampaignSettings(1, programs.Length, workers));
        _directory.WritePlan([.. programs.Select(p => new PlannedProgram(p.Id, "generated"))]);
    }

    private CampaignWorker Worker(int shard = 0, Action<string, PipelinePhase>? hook = null, Action<string>? observer = null,
            OracleBudget? budget = null) =>
        new(_directory, shard, p => _sources[p.Id], budget ?? OracleBudget.Ci, OracleBudget.Ci, new WorkerHooks(hook, observer));

    [Fact]
    public void Run_EveryProgramOfItsShard_GetsOneResultAndNoOtherShardsProgramDoes() {
        (string, string)[] programs = [.. Enumerable.Range(0, 12).Select(i => ($"p{i}", $"print({i})\n"))];
        Plan(2, programs);

        int exit = Worker(shard: 1).Run();

        Assert.Equal(0, exit);
        List<string> expected = [.. programs.Select(p => p.Item1).Where(id => CampaignPlan.ShardOf(id, 2) == 1)];
        Assert.NotEmpty(expected);
        Assert.Equal(expected, _directory.ReadResults(1).Select(r => r.ProgramId));
        Assert.All(_directory.ReadResults(1), r => Assert.Equal(OutcomeKind.Clean, r.Outcome));
        Assert.Empty(_directory.ReadResults(0));
    }

    [Fact]
    public void Run_AFinding_IsRecordedMinimisedAndStoredWithItsOriginal() {
        Plan(1, ("bad", Failing), ("good", "print(1)\n"));

        Worker().Run();

        ProgramResult bad = _directory.ReadResults(0).Single(r => r.ProgramId == "bad");
        Assert.Equal(OutcomeKind.Finding, bad.Outcome);
        Finding finding = Assert.Single(bad.Findings);
        FindingRecord record = Assert.Single(new FindingStore(_directory.FindingsPath).All());
        Assert.Equal("bad", record.ProgramId);
        Assert.Equal(finding.Signature.Key, record.Signature);
        Assert.Equal(Failing, record.Original);
        Assert.Equal("xs := [1, 2]\nprint(xs.filter(5))\n", record.Reproducer);
    }

    [Fact]
    public void Run_ProgramsWithResultsAlready_AreNotRunAgain() {
        Plan(1, ("p0", "print(0)\n"), ("p1", "print(1)\n"), ("p2", "print(2)\n"));
        _directory.AppendResult(0, new ProgramResult("p1", OutcomeKind.Clean, []));
        List<string> ran = [];

        Worker(hook: (id, phase) => { if (phase == PipelinePhase.Lex) ran.Add(id); }).Run();

        Assert.Equal(["p0", "p2"], ran);
        Assert.Equal(["p1", "p0", "p2"], _directory.ReadResults(0).Select(r => r.ProgramId));
    }

    [Fact]
    public void Run_TheJournal_NamesTheProgramAndPhaseThenTheMinimisingThenIsCleared() {
        Plan(1, ("bad", Failing));
        List<string> journal = [];

        Worker(observer: journal.Add).Run();

        Assert.Equal(["bad\tCatalogue", "bad\tLex", "bad\tParse", "bad\tCheck", "bad\tCompile", "bad\tRun", "bad\tminimise", ""], journal);
        Assert.Null(Journal.Read(_directory.JournalPath(0)));
    }

    [Fact]
    public void Run_TheJournalOnDisk_HoldsTheProgramWhileItRuns() {
        Plan(1, ("p0", "print(0)\n"));
        (string, string)? seen = null;

        Worker(hook: (_, phase) => { if (phase == PipelinePhase.Check) seen = Journal.Read(_directory.JournalPath(0)); }).Run();

        Assert.Equal(("p0", "Check"), seen);
    }

    [Fact]
    public void Run_AProgramPastItsWatchdog_IsRecordedUnminimisedAndTheWorkerAsksToBeRestarted() {
        Plan(1, ("p0", "print(0)\n"), ("stall", "print(1)\n"), ("p2", "print(2)\n"));
        new Oracle(OracleBudget.Ci).Run("print(0)\n");
        using var release = new ManualResetEventSlim();
        var budget = new OracleBudget(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(300));

        try {
            int exit = Worker(hook: (id, phase) => { if (id == "stall" && phase == PipelinePhase.Check) release.Wait(); }, budget: budget).Run();

            Assert.Equal(CampaignWorker.RestartExitCode, exit);
            IReadOnlyList<ProgramResult> results = _directory.ReadResults(0);
            Assert.Equal(["p0", "stall"], results.Select(r => r.ProgramId));
            Assert.Equal("Check|Timeout|watchdog", Assert.Single(results[1].Findings).Signature.Key);
            FindingRecord record = Assert.Single(new FindingStore(_directory.FindingsPath).All());
            Assert.Equal("print(1)\n", record.Reproducer);
            Assert.Equal(["stall\tthe run passed its watchdog, so it was not minimised"], _directory.ReadUnminimised());
            Assert.Null(Journal.Read(_directory.JournalPath(0)));
        } finally {
            release.Set();
        }
    }

    [Fact]
    public void Run_ACandidatePastItsWatchdogWhileMinimising_EndsTheMinimisingAndTheWorkerAsksToBeRestarted() {
        Plan(1, ("bad", Failing), ("after", "print(2)\n"));
        new Oracle(OracleBudget.Ci).Run("print(0)\n");
        using var release = new ManualResetEventSlim();
        var stalling = new OracleBudget(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(300));
        int checks = 0;

        try {
            var worker = new CampaignWorker(_directory, 0, p => _sources[p.Id], OracleBudget.Ci, stalling,
                new WorkerHooks(Minimise: phase => { if (phase == PipelinePhase.Check && Interlocked.Increment(ref checks) == 1) release.Wait(); }));

            int exit = worker.Run();

            Assert.Equal(CampaignWorker.RestartExitCode, exit);
            Assert.Equal(["bad"], _directory.ReadResults(0).Select(r => r.ProgramId));
            Assert.Equal(["bad\ta minimising candidate passed its watchdog"], _directory.ReadUnminimised());
            Assert.Equal(Failing, Assert.Single(new FindingStore(_directory.FindingsPath).All()).Original);
        } finally {
            release.Set();
        }
    }
}
