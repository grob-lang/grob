using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// Check 4.1.2 — decisions-log lockstep: no duplicate index code, an exact
/// bijection between index rows and full entries, and every supersession target
/// resolving. Closes the D-### collision class (D-286 is on record) and dangling
/// supersession links.
/// </summary>
public sealed class DecisionsLogLockstepTests {
    private static DecisionsLogFacts Facts(
        IReadOnlyList<string> index,
        IReadOnlyList<string> entries,
        IReadOnlyList<string>? allEntries = null,
        IReadOnlyList<string>? supersession = null)
        => new(index, entries, allEntries ?? entries, supersession ?? []);

    // --- Negative proofs ---

    [Fact]
    public void Fails_OnDuplicateIndexCode() {
        var result = ConsistencyChecks.CheckDecisionsLockstep(
            Facts(["D-1", "D-2", "D-2"], ["D-1", "D-2"]));

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("duplicate") && d.Contains("D-2"));
    }

    [Fact]
    public void Fails_WhenIndexRowHasNoFullEntry() {
        var result = ConsistencyChecks.CheckDecisionsLockstep(
            Facts(["D-1", "D-2"], ["D-1"]));

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("no matching") && d.Contains("D-2"));
    }

    [Fact]
    public void Fails_WhenFullEntryHasNoIndexRow() {
        var result = ConsistencyChecks.CheckDecisionsLockstep(
            Facts(["D-1"], ["D-1", "D-2"]));

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("no summary-index row") && d.Contains("D-2"));
    }

    [Fact]
    public void Fails_WhenSupersessionTargetDoesNotExist() {
        var result = ConsistencyChecks.CheckDecisionsLockstep(
            Facts(["D-1"], ["D-1"], allEntries: ["D-1"], supersession: ["D-999"]));

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("D-999"));
    }

    [Fact]
    public void Passes_WhenSupersessionTargetIsAPostMvpEntry() {
        // D-PM-### entries are index-exempt but are valid supersession targets.
        var result = ConsistencyChecks.CheckDecisionsLockstep(
            Facts(["D-1"], ["D-1"], allEntries: ["D-1", "D-PM-001"], supersession: ["D-PM-001"]));

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
    }

    // --- Positive proof: the live decisions log holds lockstep ---

    [Fact]
    public void LiveDecisionsLog_HoldsLockstep() {
        var facts = ConsistencyChecks.ParseDecisionsLog(RepoPaths.DecisionsLog);
        var result = ConsistencyChecks.CheckDecisionsLockstep(facts);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
    }

    [Fact]
    public void ParsingExtractsEveryTargetOnAMultiTargetSupersessionLine() {
        // A single line can list several targets ("Superseded by: D-288, D-291");
        // both must be extracted, not just the first (the log has such lines).
        var path = Path.Join(Path.GetTempPath(), $"drift-log-{Guid.NewGuid():N}.md");
        File.WriteAllText(path,
            "| D-1 | June 2026 | Area | desc |\n\n### D-1 — Title\nSuperseded by: D-288, D-291\n");
        try {
            var facts = ConsistencyChecks.ParseDecisionsLog(path);

            Assert.Contains("D-288", facts.SupersessionTargets);
            Assert.Contains("D-291", facts.SupersessionTargets);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void LiveDecisionsLog_ExcludesPostMvpEntryFromTheIndexBijection() {
        var facts = ConsistencyChecks.ParseDecisionsLog(RepoPaths.DecisionsLog);

        Assert.Contains("D-PM-001", facts.AllEntryCodes);
        Assert.DoesNotContain("D-PM-001", facts.EntryCodes);
        Assert.DoesNotContain("D-PM-001", facts.IndexCodes);
    }
}
