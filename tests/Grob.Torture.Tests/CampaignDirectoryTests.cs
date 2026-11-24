using Grob.Torture.Campaign;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// A campaign's files: its settings and plan, each shard's results and the journal a worker keeps
/// so that its supervisor can name the program it died on (gate report §4.4).
/// </summary>
public sealed class CampaignDirectoryTests : IDisposable {
    private readonly string _root = Path.Join(Path.GetTempPath(), $"grob-campaign-{Guid.NewGuid():N}");

    public void Dispose() {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static ProgramResult Result(string id, OutcomeKind outcome = OutcomeKind.Clean, params Finding[] findings) =>
        new(id, outcome, findings);

    [Fact]
    public void Settings_RoundTrip() {
        var directory = new CampaignDirectory(_root);
        var settings = new CampaignSettings(20261124, 200000, 4);

        directory.WriteSettings(settings);

        Assert.Equal(settings, new CampaignDirectory(_root).ReadSettings());
    }

    [Fact]
    public void ReadSettings_NewDirectory_IsNull() {
        Assert.Null(new CampaignDirectory(_root).ReadSettings());
    }

    [Fact]
    public void Plan_RoundTripsInOrder() {
        var directory = new CampaignDirectory(_root);
        PlannedProgram[] plan = [new("generated/0000000000000007/0", "generated"), new("tests/a.cs@00ff/swap-operands/3", "swap-operands")];

        directory.WritePlan(plan);

        Assert.Equal(plan, directory.ReadPlan());
    }

    [Fact]
    public void Results_RoundTripEveryField() {
        var directory = new CampaignDirectory(_root);
        var finding = new Finding(new Signature(PipelinePhase.Run, FindingClass.Exception, "GrobInternalException: boom"), "boom 3", "Grob.Vm.VirtualMachine.Run");
        ProgramResult written = Result("p@1/x/0", OutcomeKind.Finding, finding);

        directory.AppendResult(2, Result("p@0/x/0"));
        directory.AppendResult(2, written);

        IReadOnlyList<ProgramResult> read = directory.ReadResults(2);
        Assert.Equal(2, read.Count);
        Assert.Equal("p@1/x/0", read[1].ProgramId);
        Assert.Equal(OutcomeKind.Finding, read[1].Outcome);
        Assert.Equal(finding, Assert.Single(read[1].Findings));
    }

    [Fact]
    public void ReadResults_ShardWithNoFile_IsEmpty() {
        Assert.Empty(new CampaignDirectory(_root).ReadResults(0));
    }

    [Fact]
    public void ReadResults_ALineCutShortByADeath_IsSkippedAndTheNextAppendStartsANewLine() {
        var directory = new CampaignDirectory(_root);
        directory.AppendResult(0, Result("first"));
        File.AppendAllText(directory.ResultsPath(0), "{\"programId\":\"cut");

        directory.AppendResult(0, Result("second"));

        Assert.Equal(["first", "second"], directory.ReadResults(0).Select(r => r.ProgramId));
    }

    [Fact]
    public void Unminimised_RoundTripInOrder() {
        var directory = new CampaignDirectory(_root);

        directory.AppendUnminimised("p@1/x/0", "the worker died while minimising, exit 0xC00000FD");
        directory.AppendUnminimised("p@2/x/0", "a candidate passed its watchdog");

        Assert.Equal(["p@1/x/0\tthe worker died while minimising, exit 0xC00000FD", "p@2/x/0\ta candidate passed its watchdog"],
            directory.ReadUnminimised());
    }

    [Fact]
    public void ReadUnminimised_NoFile_IsEmpty() {
        Assert.Empty(new CampaignDirectory(_root).ReadUnminimised());
    }

    [Fact]
    public void Journal_HoldsOnlyTheLatestWrite() {
        string path = new CampaignDirectory(_root).JournalPath(1);
        using var journal = new Journal(path);

        journal.Write("a-long-program-id@0123456789abcdef/swap-operands/7", "Check");
        journal.Write("p@1/x/0", "Run");

        Assert.Equal(("p@1/x/0", "Run"), Journal.Read(path));
    }

    [Fact]
    public void Journal_Cleared_ReadsAsEmpty() {
        string path = new CampaignDirectory(_root).JournalPath(1);
        using var journal = new Journal(path);
        journal.Write("p@1/x/0", "Run");

        journal.Clear();

        Assert.Null(Journal.Read(path));
    }

    [Fact]
    public void Journal_WrittenThenAbandoned_IsReadableWhileStillOpen() {
        string path = new CampaignDirectory(_root).JournalPath(0);
        using var journal = new Journal(path);

        journal.Write("p@1/x/0", Journal.Minimising);

        Assert.Equal(("p@1/x/0", Journal.Minimising), Journal.Read(path));
    }

    [Fact]
    public void JournalRead_NoFile_IsNull() {
        Assert.Null(Journal.Read(new CampaignDirectory(_root).JournalPath(3)));
    }

    [Fact]
    public void Journal_OpenedAgain_StartsEmpty() {
        string path = new CampaignDirectory(_root).JournalPath(0);
        using (var first = new Journal(path)) first.Write("p@1/x/0", "Run");

        using var second = new Journal(path);

        Assert.Null(Journal.Read(path));
    }
}
