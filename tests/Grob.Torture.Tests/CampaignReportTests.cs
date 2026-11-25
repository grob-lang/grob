using Grob.Torture.Campaign;
using Grob.Torture.Findings;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The campaign's report gives the prompt §5 figures (programs by source and operator, findings by
/// signature) and lays out each signature's distinct reproducers for triage, shortest first.
/// </summary>
public sealed class CampaignReportTests {
    private const string Boom = "Run|Exception|GrobInternalException: boom";
    private const string Crash = "Compile|Exception|InvalidOperationException: crash";

    private static readonly CampaignSettings Settings = new(5, 6, 2);

    private static Finding F(string key) {
        string[] parts = key.Split('|', 3);
        return new Finding(new Signature(Enum.Parse<PipelinePhase>(parts[0]), Enum.Parse<FindingClass>(parts[1]), parts[2]), "message", "Grob.Vm.VirtualMachine.Run");
    }

    private static readonly ProgramResult[] Results = [
        new("generated/0000000000000005/0", OutcomeKind.Clean, []),
        new("generated/0000000000000005/1", OutcomeKind.Finding, [F(Boom)]),
        new("generated/0000000000000005/2", OutcomeKind.RuntimeError, []),
        new("tests/a.cs@00ff/swap-operands/0", OutcomeKind.Finding, [F(Boom), F(Crash)]),
        new("tests/a.cs@00ff/swap-operands/1", OutcomeKind.Diagnostics, []),
        new("b.grob@01/replace-value/3", OutcomeKind.Finding, [F(Boom)]),
    ];

    private static readonly FindingRecord[] Records = [
        new("generated/0000000000000005/1", Boom, "m", null, "long original\n", "x := long()\nprint(x)\n", false),
        new("tests/a.cs@00ff/swap-operands/0", Boom, "m", null, "o\n", "x()\n", true),
        new("b.grob@01/replace-value/3", Boom, "m", null, "o\n", "x()\n", false),
        new("tests/a.cs@00ff/swap-operands/0", Crash, "m", "Grob.Compiler.Compiler.Emit", "o\n", "y\n", false),
    ];

    private static string Build(KnownFindings? known = null, IReadOnlyList<string>? unminimised = null, IReadOnlyList<BlackBoxResult>? blackBox = null) =>
        CampaignReport.Build(Settings, Results, Records, unminimised ?? [], known, blackBox ?? []).ReplaceLineEndings("\n");

    [Fact]
    public void Build_ProgramsTable_CountsOutcomesBySourceThenAll() {
        string report = Build();

        Assert.Contains(
            "| Source | Programs | Diagnostics | Clean | Exit | RuntimeError | Bounded | Finding |\n" +
            "|---|---|---|---|---|---|---|---|\n" +
            "| generated | 3 | 0 | 1 | 0 | 1 | 0 | 1 |\n" +
            "| replace-value | 1 | 0 | 0 | 0 | 0 | 0 | 1 |\n" +
            "| swap-operands | 2 | 1 | 0 | 0 | 0 | 0 | 1 |\n" +
            "| **all** | 6 | 1 | 1 | 0 | 1 | 0 | 3 |\n",
            report, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_SignatureTable_CountsProgramsReproducersAndFlagsMostProgramsFirst() {
        string report = Build();

        Assert.Contains("2 signatures over 3 programs.", report, StringComparison.Ordinal);
        int boom = report.IndexOf("| Run\\|Exception\\|GrobInternalException: boom | 3 | 1 | 2 | 2 | 1 | — |", StringComparison.Ordinal);
        int crash = report.IndexOf("| Compile\\|Exception\\|InvalidOperationException: crash | 1 | 0 | 1 | 1 | 0 | — |", StringComparison.Ordinal);
        Assert.True(boom >= 0, report);
        Assert.True(crash > boom, report);
    }

    [Fact]
    public void Build_KnownSignature_NamesItsRows() {
        var known = new KnownFindings([new KnownEntry("R-60", Boom, ["p"]), new KnownEntry("R-63", Boom, ["q"]), new KnownEntry("R-60", Boom, ["r"])]);

        string report = Build(known);

        Assert.Contains("| Run\\|Exception\\|GrobInternalException: boom | 3 | 1 | 2 | 2 | 1 | R-60, R-63 |", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EachSignature_ListsItsDistinctReproducersShortestFirstWithTheirPrograms() {
        string report = Build();

        int section = report.IndexOf("### Run|Exception|GrobInternalException: boom\n", StringComparison.Ordinal);
        int shortest = report.IndexOf("2 programs, first `b.grob@01/replace-value/3`:\n\n```grob\nx()\n```\n", section, StringComparison.Ordinal);
        int longer = report.IndexOf("1 program, first `generated/0000000000000005/1`:\n\n```grob\nx := long()\nprint(x)\n```\n", section, StringComparison.Ordinal);
        Assert.True(section >= 0, report);
        Assert.True(shortest > section, report);
        Assert.True(longer > shortest, report);
        Assert.Contains("First frame: `Grob.Compiler.Compiler.Emit`.", report, StringComparison.Ordinal);
    }

    // A reproducer can hold a run of backticks, in a string or as a raw string's delimiter, so its
    // fence must be longer than any run in the source or the block ends early.
    [Fact]
    public void Build_ReproducerHoldingABacktickRun_IsFencedByALongerOne() {
        FindingRecord[] fenced = [new("p", Boom, "m", null, "o\n", "s := \"\n```\n````\n\"\n", false)];

        string report = CampaignReport.Build(Settings, Results, fenced, [], null, []).ReplaceLineEndings("\n");

        Assert.Contains("1 program, first `p`:\n\n`````grob\ns := \"\n```\n````\n\"\n`````\n", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_ManyReproducers_ListsTheShortestFewAndCountsTheRest() {
        FindingRecord[] many = [.. Enumerable.Range(0, CampaignReport.ReproducersShown + 2)
            .Select(i => new FindingRecord($"p{i}", Boom, "m", null, "o\n", new string('x', i + 1) + "\n", false))];

        string report = CampaignReport.Build(Settings, Results, many, [], null, []).ReplaceLineEndings("\n");

        Assert.Contains("and 2 more distinct reproducers.", report, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', CampaignReport.ReproducersShown + 1) + "\n```", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_Unminimised_AreListed() {
        string report = Build(unminimised: ["p@1/x/0\tthe worker died running it, exit 0xC00000FD"]);

        Assert.Contains("## Not minimised\n\n- `p@1/x/0`: the worker died running it, exit 0xC00000FD\n", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_NothingUnminimisedAndNoBlackBox_SaysSo() {
        string report = Build();

        Assert.Contains("## Not minimised\n\nNone.\n", report, StringComparison.Ordinal);
        Assert.Contains("## Black-box check\n\nNot run.\n", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_BlackBox_GroupsBySignatureVerdictAndExitCodeCountingPrograms() {
        BlackBoxResult[] blackBox = [
            new(Boom, "x()\n", BlackBoxVerdict.Reported, 1),
            new(Boom, "x := long()\nprint(x)\n", BlackBoxVerdict.Reported, 1),
            new(Crash, "y\n", BlackBoxVerdict.StackTrace, -532462766),
        ];

        string report = Build(blackBox: blackBox);

        Assert.Contains("| Run\\|Exception\\|GrobInternalException: boom | Reported | 1 | 3 |", report, StringComparison.Ordinal);
        Assert.Contains("| Compile\\|Exception\\|InvalidOperationException: crash | StackTrace | -532462766 | 1 |", report, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_Header_NamesTheSeedAndSize() {
        Assert.StartsWith("# Crash oracle campaign\n\nCampaign seed 5, 6 programs, 2 workers.\n", Build(), StringComparison.Ordinal);
    }
}
