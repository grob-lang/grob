using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// Check 4.1.1 — error-code count agreement. The count stated in the registry,
/// the summary-index count and ErrorCatalog must all be the same number. This is
/// what closes the 86 / 94 / 98 / 99 stale-count class permanently (D-316).
/// </summary>
public sealed class ErrorCodeCountTests {
    // --- Negative proof: planted violations must fail with a clear message ---

    [Fact]
    public void Comparison_FailsWhenFooterTotalIsStale() {
        // The exact historical drift this gate exists to catch: footer said 99
        // while the catalog and index held 103.
        var result = ConsistencyChecks.CompareErrorCodeCount(
            summaryIndexCount: 103, catalogCount: 103, footerTotal: 99);

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("canonical total states 99") && d.Contains("103"));
    }

    [Fact]
    public void Comparison_FailsWhenSummaryIndexDisagreesWithCatalog() {
        var result = ConsistencyChecks.CompareErrorCodeCount(
            summaryIndexCount: 102, catalogCount: 103, footerTotal: 103);

        Assert.False(result.Ok);
        Assert.Contains(result.Discrepancies, d => d.Contains("summary index has 102") && d.Contains("103"));
    }

    // --- Positive proof: the reconciled corpus agrees ---

    [Fact]
    public void SummaryIndex_FooterTotal_AndCatalog_AllAgree() {
        var summary = ConsistencyChecks.ParseSummaryIndexCount(RepoPaths.ErrorCodes);
        var footer = ConsistencyChecks.ParseFooterTotal(RepoPaths.ErrorCodes);
        var catalog = ConsistencyChecks.ActualErrorCatalogCount();

        var result = ConsistencyChecks.CompareErrorCodeCount(summary, catalog, footer);

        Assert.True(result.Ok,
            $"Count drift: summary={summary}, footer={footer}, catalog={catalog}. " +
            string.Join("; ", result.Discrepancies));
    }

    [Fact]
    public void Corpus_HasTheExpectedLiveCountOf111() {
        // A standing anchor: if the count legitimately changes, this and the
        // canonical footer line move together, by intent, in the same change.
        // D-320 added E1103 (reserved identifier used as a binding name): 107 -> 108.
        // D-323 added E0303 (circular type dependency among top-level value bindings): 108 -> 109.
        // D-330 added E0012 (unknown field name) and E0013 (field default references sibling field): 109 -> 111.
        Assert.Equal(111, ConsistencyChecks.ActualErrorCatalogCount());
        Assert.Equal(111, ConsistencyChecks.ParseSummaryIndexCount(RepoPaths.ErrorCodes));
        Assert.Equal(111, ConsistencyChecks.ParseFooterTotal(RepoPaths.ErrorCodes));
    }
}
