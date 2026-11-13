using Xunit;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The quarantine manifest, <c>docs/errors/examples/quarantine.md</c> (D-428 Decision 4): its
/// grammar, each rejection, and the guards that keep a row honest — an <c>R-##</c> must be an open
/// register row and a namespace must be absent from the stdlib catalogue.
/// </summary>
public sealed class QuarantineManifestTests {
    private const string Header = "| Pair | Category | Blocked by | Reason | Observed |\n|---|---|---|---|---|\n";

    private const string Register =
        "### Open items\n\n| # | Item | Raised by | Owner | Done when |\n|---|---|---|---|---|\n" +
        "| R-26 | **Columns.** | D-428 | hardening | done |\n\n" +
        "### Closed items\n\n| # | Item | Raised by | Closed by | How |\n|---|---|---|---|---|\n" +
        "| R-01 | **E4202 removed.** | D-410 | **D-425** | gone |\n\n" +
        "### OQ-005 — Value Representation\n";

    private static readonly HashSet<string> Slugs = new(["a", "b"], StringComparer.Ordinal);

    private static QuarantineManifest Parse(params string[] rows) =>
        QuarantineManifest.Parse("# Quarantine\n\nProse.\n\n" + Header + string.Concat(rows.Select(r => r + "\n")),
            Slugs, DeferredWorkRegister.Parse(Register), ns => ns == "math");

    private static string OnlyProblem(params string[] rows) => Assert.Single(Parse(rows).Problems);

    [Fact]
    public void Parse_WellFormedRows_ReadsEachCell() {
        QuarantineManifest manifest = Parse(
            "| `a` | gap | R-26 | Columns are not recorded. | error[E5101] 4 |",
            "| `b` | unbuilt | `fs`, Sprint 11 | The fs module is not built. | none |");

        Assert.Empty(manifest.Problems);
        QuarantineRow a = manifest.Rows["a"];
        Assert.Equal(7, a.LineNumber);
        Assert.Equal("gap", a.Category);
        Assert.Equal(["R-26"], a.BlockedBy);
        Assert.Equal("Columns are not recorded.", a.Reason);
        Assert.Equal([new ExampleDiagnostic("error", "E5101", "a.grob", 4, null)], a.Observed);
        Assert.Equal(["`fs`", "Sprint 11"], manifest.Rows["b"].BlockedBy);
        Assert.Empty(manifest.Rows["b"].Observed);
    }

    [Fact]
    public void Parse_TableEndsAtFirstNonRowLine() {
        QuarantineManifest manifest = Parse("| `a` | gap | R-26 | Columns. | none |", "", "Trailing prose | with a bar.");

        Assert.Empty(manifest.Problems);
        Assert.Single(manifest.Rows);
    }

    [Fact]
    public void Parse_MissingHeader_Fails() {
        QuarantineManifest manifest = QuarantineManifest.Parse("# Quarantine\n\n| Pair | Category |\n", Slugs,
            DeferredWorkRegister.Parse(Register), _ => false);

        string problem = Assert.Single(manifest.Problems);
        Assert.Equal("quarantine.md: expected the header row \"| Pair | Category | Blocked by | Reason | Observed |\"", problem);
    }

    [Fact]
    public void Parse_WrongCellCount_Fails() {
        Assert.Equal("quarantine.md:7: row has 4 cells, expected 5", OnlyProblem("| `a` | gap | R-26 | none |"));
    }

    [Fact]
    public void Parse_UnknownPair_Fails() {
        Assert.Equal("quarantine.md:7: pair `zz` has no directory under docs/errors/examples/",
            OnlyProblem("| `zz` | gap | R-26 | Columns. | none |"));
    }

    [Fact]
    public void Parse_UnbacktickedPair_Fails() {
        Assert.Equal("quarantine.md:7: pair cell `a` is not a backticked slug",
            OnlyProblem("| a | gap | R-26 | Columns. | none |"));
    }

    [Fact]
    public void Parse_DuplicatePair_Fails() {
        Assert.Equal("quarantine.md:8: pair `a` is already quarantined on line 7",
            OnlyProblem("| `a` | gap | R-26 | Columns. | none |", "| `a` | gap | R-26 | Columns. | none |"));
    }

    [Fact]
    public void Parse_UnknownCategory_Fails() {
        Assert.Equal("quarantine.md:7: category `flaky` is not one of unbuilt, gap, divergent or stale",
            OnlyProblem("| `a` | flaky | R-26 | Columns. | none |"));
    }

    [Theory]
    [InlineData("R-2", "R-2")]
    [InlineData("fs", "fs")]
    [InlineData("Sprint eleven", "Sprint eleven")]
    [InlineData("R-26,R-27", "R-26,R-27")]
    public void Parse_UnparseableBlockedBy_Fails(string cell, string item) {
        Assert.Equal($"quarantine.md:7: Blocked by item `{item}` is not an R-## register item, a `namespace` or Sprint N",
            OnlyProblem($"| `a` | gap | {cell} | Columns. | none |"));
    }

    [Fact]
    public void Parse_EmptyBlockedBy_Fails() {
        Assert.Equal("quarantine.md:7: Blocked by is empty", OnlyProblem("| `a` | gap |  | Columns. | none |"));
    }

    [Fact]
    public void Parse_EmptyReason_Fails() {
        Assert.Equal("quarantine.md:7: Reason is empty", OnlyProblem("| `a` | gap | R-26 |  | none |"));
    }

    [Fact]
    public void Parse_UnparseableObserved_Fails() {
        Assert.Equal("quarantine.md:7: Observed entry `E5101 at 4` is not severity[Exxxx] [file:]line[:column]",
            OnlyProblem("| `a` | gap | R-26 | Columns. | E5101 at 4 |"));
    }

    [Fact]
    public void Parse_EmptyObserved_Fails() {
        Assert.Equal("quarantine.md:7: Observed is empty; write `none` for a pair that produces no diagnostics",
            OnlyProblem("| `a` | gap | R-26 | Columns. |  |"));
    }

    [Fact]
    public void Parse_ClosedRegisterItem_Fails() {
        Assert.Equal("quarantine.md:7: R-01 is closed (by D-425); re-classify pair `a` or remove its row",
            OnlyProblem("| `a` | gap | R-01 | Columns. | none |"));
    }

    [Fact]
    public void Parse_AbsentRegisterItem_Fails() {
        Assert.Equal("quarantine.md:7: R-99 is not a row of the Deferred Work Register",
            OnlyProblem("| `a` | gap | R-99 | Columns. | none |"));
    }

    [Fact]
    public void Parse_RegisteredNamespace_Fails() {
        Assert.Equal("quarantine.md:7: namespace `math` is registered in the stdlib catalogue; re-classify pair `a`",
            OnlyProblem("| `a` | unbuilt | `math` | Unbuilt. | none |"));
    }

    [Fact]
    public void Parse_UnregisteredNamespaceAndSprint_Pass() {
        Assert.Empty(Parse("| `a` | unbuilt | `fs`, `json`, Sprint 11 | Unbuilt. | none |").Problems);
    }
}

/// <summary>The Deferred Work Register read for the manifest's open-row guard.</summary>
public sealed class DeferredWorkRegisterTests {
    private const string Text =
        "### Open items\n\n| # | Item |\n|---|---|\n| R-05 | **Audit.** |\n| R-26 | **Columns.** |\n\n" +
        "### Closed items\n\n| # | Item | Raised by | Closed by | How |\n|---|---|---|---|---|\n" +
        "| R-01 | **E4202 removed.** | D-410 | **D-425** | gone |\n\n### OQ-005 — later\n\n| R-77 | not a register row |\n";

    [Fact]
    public void Parse_ReadsOpenAndClosedRows() {
        DeferredWorkRegister register = DeferredWorkRegister.Parse(Text);

        Assert.Equal(["R-05", "R-26"], register.Open.Order(StringComparer.Ordinal));
        Assert.Equal("D-425", Assert.Single(register.ClosedBy, kv => kv.Key == "R-01").Value);
        Assert.DoesNotContain("R-77", register.Open);
        Assert.False(register.ClosedBy.ContainsKey("R-77"));
    }
}

/// <summary>
/// The verdict for one pair (D-428 Decision 4): an unquarantined pair must match its master; a
/// quarantined one must fail exactly as its <em>Observed</em> cell records — strictness is against
/// the recorded list, not against "any failure".
/// </summary>
public sealed class PairVerdictTests {
    private const string Slug = "p";

    private static ExampleDiagnostic Error(string code, int line, int? column) => new("error", code, "p.grob", line, column);

    private static readonly IReadOnlyList<ExampleDiagnostic> Master = [Error("E5101", 4, 7)];
    private static readonly IReadOnlyList<ExampleDiagnostic> Recorded = [Error("E5101", 4, null)];

    private static QuarantineRow Row(IReadOnlyList<ExampleDiagnostic> observed) =>
        new(7, Slug, "gap", ["R-26"], "Columns are not recorded.", observed);

    [Fact]
    public void Check_UnquarantinedMatch_Passes() {
        Assert.Null(PairVerdict.Check(Slug, Master, Master, row: null));
    }

    [Fact]
    public void Check_UnquarantinedMismatch_FailsWithBothLists() {
        Assert.Equal("expected: error[E5101] 4:7\nactual:   error[E5101] 4", PairVerdict.Check(Slug, Master, Recorded, row: null));
    }

    [Fact]
    public void Check_QuarantinedFailingAsRecorded_Passes() {
        Assert.Null(PairVerdict.Check(Slug, Master, Recorded, Row(Recorded)));
    }

    [Fact]
    public void Check_QuarantinedPairThatPasses_FailsAskingForRowRemoval() {
        Assert.Equal("pair `p` is quarantined but now matches its master; remove its row from quarantine.md",
            PairVerdict.Check(Slug, Master, Master, Row(Recorded)));
    }

    // Recorded is error[E5101] 4; each row drifts from it in one respect.
    [Theory]
    [InlineData("error[E5102] 4")] // code
    [InlineData("error[E5101] 5")] // line
    [InlineData("error[E5101] 4:3")] // column
    [InlineData("warning[E5101] 4")] // severity
    [InlineData("error[E5101] 4; error[E1001] 6:1")] // count
    [InlineData("none")] // no diagnostics at all
    public void Check_QuarantinedPairObservedDrift_FailsWithRecordedAndActual(string actualEncoded) {
        Assert.True(ExampleDiagnostic.TryDecodeList(actualEncoded, "p.grob", out IReadOnlyList<ExampleDiagnostic>? actual, out _));

        Assert.Equal(
            $"pair `p` no longer fails as quarantine.md records\nrecorded: error[E5101] 4\nactual:   {actualEncoded}",
            PairVerdict.Check(Slug, Master, actual, Row(Recorded)));
    }
}
