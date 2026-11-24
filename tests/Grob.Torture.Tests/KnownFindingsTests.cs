using Xunit;

using Grob.Integration.Tests.ErrorExamples;

namespace Grob.Torture.Tests;

/// <summary>
/// The known-findings list keeps the slice green while a register row is open (prompt §1.4).
/// A signature does not identify a root cause, so an entry names its row and the programs it
/// covers, and only those programs are suppressed.
/// </summary>
public sealed class KnownFindingsTests {
    private const string Register = """
        ### Open items

        | # | Item | Raised by | Owner | Done when |
        |---|---|---|---|---|
        | R-70 | **Open.** | D-451 | `unowned` | x |

        ### Closed items

        | # | Item | Raised by | Closed by | Evidence |
        |---|---|---|---|---|
        | R-61 | **Closed.** | D-449 | **D-450** | y |
        """;

    private static readonly Signature Sig = new(PipelinePhase.Run, FindingClass.Exception, "GrobInternalException: boom");

    private static KnownFindings Known(string row, params string[] programs) =>
        KnownFindings.Parse($$"""
            { "entries": [ { "row": "{{row}}", "signature": "{{Sig.Key}}", "programs": [{{string.Join(", ", programs.Select(p => $"\"{p}\""))}}] } ] }
            """);

    private static ProgramResult Failing(string program) =>
        new(program, OutcomeKind.Finding, [new Finding(Sig, "boom")]);

    [Fact]
    public void Problems_EntryOnAnOpenRow_IsNone() {
        Assert.Empty(Known("R-70", "a@1").Problems(DeferredWorkRegister.Parse(Register)));
    }

    [Fact]
    public void Problems_EntryWithoutARow_IsReported() {
        string problem = Assert.Single(Known("R-99", "a@1").Problems(DeferredWorkRegister.Parse(Register)));

        Assert.Contains("R-99", problem, StringComparison.Ordinal);
        Assert.Contains("not in the register", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Problems_EntryOnAClosedRow_IsReported() {
        string problem = Assert.Single(Known("R-61", "a@1").Problems(DeferredWorkRegister.Parse(Register)));

        Assert.Contains("closed by D-450", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Problems_EntryWithNoPrograms_IsReported() {
        string problem = Assert.Single(Known("R-70").Problems(DeferredWorkRegister.Parse(Register)));

        Assert.Contains("names no programs", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Judge_ListedProgram_IsSuppressed() {
        SliceVerdict verdict = CorpusSlice.Judge([Failing("a@1")], Known("R-70", "a@1"));

        Assert.Empty(verdict.Unknown);
        Assert.Empty(verdict.Stale);
    }

    [Fact]
    public void Judge_UnlistedProgramWithAKnownSignature_IsReported() {
        SliceVerdict verdict = CorpusSlice.Judge([Failing("a@1"), Failing("b@2")], Known("R-70", "a@1"));

        (string program, Finding finding) = Assert.Single(verdict.Unknown);
        Assert.Equal("b@2", program);
        Assert.Equal(Sig, finding.Signature);
    }

    [Fact]
    public void Judge_ListedProgramThatNoLongerFails_IsStale() {
        SliceVerdict verdict = CorpusSlice.Judge(
            [new ProgramResult("a@1", OutcomeKind.Clean, [])], Known("R-70", "a@1"));

        Assert.Equal([$"R-70 a@1 {Sig.Key}: no longer makes this finding"], verdict.Stale);
    }

    [Fact]
    public void Judge_ListedProgramNotInTheRun_IsStale() {
        SliceVerdict verdict = CorpusSlice.Judge([], Known("R-70", "a@1"));

        Assert.Equal([$"R-70 a@1 {Sig.Key}: not in the corpus"], verdict.Stale);
    }

    [Fact]
    public void Parse_EmptyList_HasNoEntries() {
        Assert.Empty(KnownFindings.Parse("""{ "entries": [] }""").Entries);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("""{ "entries": [null] }""")]
    [InlineData("""{ "entries": [ { "row": "R-70", "signature": "s" } ] }""")]
    [InlineData("""{ "entries": [ { "row": null, "signature": "s", "programs": [] } ] }""")]
    public void Parse_MalformedList_Throws(string json) {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => KnownFindings.Parse(json));
    }
}
