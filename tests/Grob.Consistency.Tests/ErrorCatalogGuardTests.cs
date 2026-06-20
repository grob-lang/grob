using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// Check 4.1.5 — the D-308 ErrorCatalog↔registry agreement test is present and
/// discoverable, so the consistency suite is the single index of every mechanical
/// agreement check in the build. This does not re-run D-308's assertions.
/// </summary>
public sealed class ErrorCatalogGuardTests {
    // --- Negative proof: a search root without the guard fails ---

    [Fact]
    public void Fails_WhenTheGuardFileIsAbsent() {
        var empty = Directory.CreateTempSubdirectory("drift-guard-absent-");
        try {
            var result = ConsistencyChecks.CheckErrorCatalogGuardPresent(empty.FullName);

            Assert.False(result.Ok);
            Assert.Contains(result.Discrepancies, d => d.Contains("D-308") || d.Contains("not found"));
        } finally {
            empty.Delete(recursive: true);
        }
    }

    [Fact]
    public void Fails_WhenTheGuardNoLongerReferencesBothSides() {
        var root = Directory.CreateTempSubdirectory("drift-guard-hollow-");
        try {
            var dir = Directory.CreateDirectory(Path.Join(root.FullName, "tests", "Grob.Core.Tests"));
            File.WriteAllText(
                Path.Join(dir.FullName, "ErrorCatalogAgreementTests.cs"),
                "// a file that no longer guards anything");

            var result = ConsistencyChecks.CheckErrorCatalogGuardPresent(root.FullName);

            Assert.False(result.Ok);
            Assert.Contains(result.Discrepancies, d =>
                d.Contains("ErrorCatalog") && d.Contains("grob-error-codes.md"));
        } finally {
            root.Delete(recursive: true);
        }
    }

    // --- Positive proof: the live guard is present ---

    [Fact]
    public void LiveGuard_IsPresentAndReferencesCatalogAndRegistry() {
        var result = ConsistencyChecks.CheckErrorCatalogGuardPresent(RepoPaths.RepoRoot());

        Assert.True(result.Ok, string.Join("; ", result.Discrepancies));
    }
}
