using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// Check 4.1.3 — ADR reference integrity. Every ADR-00NN mentioned in the design
/// corpus must resolve to a published file under docs/wiki/ADR. Closes the
/// ADR-0007 → 0012 / ADR-0008 → 0013 renumbering drift class.
/// </summary>
public sealed class AdrReferenceTests {
    // --- Negative proof ---

    [Fact]
    public void Fails_WhenAReferenceHasNoPublishedAdr() {
        var references = new[] {
            new AdrReference("grob-decisions-log.md", 42, "0012"),
            new AdrReference("grob-v1-requirements.md", 7, "9999"),
        };
        var available = new HashSet<string> { "0012" };

        var result = ConsistencyChecks.CheckAdrReferences(references, available);

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("ADR-9999") && d.Contains("grob-v1-requirements.md:7"));
    }

    // --- Positive proof: every live reference resolves ---

    [Fact]
    public void EveryLiveAdrReference_ResolvesToAFile() {
        var references = ConsistencyChecks.ParseAdrReferences(RepoPaths.DesignDir);
        var available = ConsistencyChecks.ParseAvailableAdrs(RepoPaths.AdrDir);

        var result = ConsistencyChecks.CheckAdrReferences(references, available);

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
    }

    [Fact]
    public void ParsingFindsReferences_AndPublishedAdrs() {
        // Defensive-parsing guard: a green result must mean "checked", not "found nothing".
        Assert.NotEmpty(ConsistencyChecks.ParseAdrReferences(RepoPaths.DesignDir));
        Assert.NotEmpty(ConsistencyChecks.ParseAvailableAdrs(RepoPaths.AdrDir));
    }
}
