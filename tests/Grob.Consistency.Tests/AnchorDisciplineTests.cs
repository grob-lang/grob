using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// Check 4.2 — parsing discipline. A check that cannot locate its anchor must
/// fail loudly, naming the document and the expected anchor, never pass by
/// silently finding nothing. A green result must mean "checked and agreed".
/// </summary>
public sealed class AnchorDisciplineTests {
    private static string TempFile(string content) {
        var path = Path.Combine(Path.GetTempPath(), $"drift-anchor-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void SummaryIndexCount_ThrowsWhenTheSectionIsMissing() {
        var path = TempFile("# Error codes\n\nNo summary index here.\n");
        try {
            var ex = Assert.Throws<AnchorNotFoundException>(
                () => ConsistencyChecks.ParseSummaryIndexCount(path));
            Assert.Contains("Summary Index", ex.Anchor);
            Assert.Equal(Path.GetFileName(path), ex.Document);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void FooterTotal_ThrowsWhenTheCanonicalLineIsMissing() {
        var path = TempFile("# Error codes\n\n_Some prose with 42 codes mentioned but no canonical line._\n");
        try {
            var ex = Assert.Throws<AnchorNotFoundException>(
                () => ConsistencyChecks.ParseFooterTotal(path));
            Assert.Contains("Total", ex.Anchor);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void DecisionsLog_ThrowsWhenNeitherIndexNorEntriesAreFound() {
        var path = TempFile("# Decisions\n\nNothing structured here.\n");
        try {
            Assert.Throws<AnchorNotFoundException>(
                () => ConsistencyChecks.ParseDecisionsLog(path));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void SpecOpCodes_ThrowWhenTheBlockIsMissing() {
        var path = TempFile("# Requirements\n\n### 3.3 OpCode\n\nProse, no csharp block.\n\n### 3.4 Tokens\n");
        try {
            Assert.Throws<AnchorNotFoundException>(
                () => ConsistencyChecks.ParseSpecOpCodes(path));
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void SpecTokenAtoms_ThrowWhenTheListingIsMissing() {
        var path = TempFile("# Requirements\n\n### 3.4 Token Kind\n\nProse, no fenced listing.\n\n### 3.5 Next\n");
        try {
            Assert.Throws<AnchorNotFoundException>(
                () => ConsistencyChecks.ParseSpecTokenAtoms(path));
        } finally {
            File.Delete(path);
        }
    }
}
