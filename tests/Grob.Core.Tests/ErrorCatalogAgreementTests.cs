using System.Text.RegularExpressions;
using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

/// <summary>
/// Guards the hand-maintained <see cref="ErrorCatalog"/> against the error-code
/// registry (grob-error-codes.md). Every registered code must have a descriptor,
/// every descriptor must be registered, and the titles must match exactly. This
/// is what makes hand-maintenance safe and ADR-0017 (code immutability)
/// enforceable rather than aspirational. See D-308.
/// </summary>
public sealed class ErrorCatalogAgreementTests {
    // Resolved relative to the repo so the test reads the canonical registry.
    // Adjust the walk-up if the test project sits at a different depth.
    private static readonly string _registryPath =
        LocateRegistry("grob-error-codes.md");

    private sealed record RegistryRow(string Code, string Title, string Category, string Status);

    private static IReadOnlyList<RegistryRow> ParseSummaryIndex() {
        var rows = new List<RegistryRow>();
        var seen = new HashSet<string>();
        var rowPattern = new Regex(
            @"^\|\s*(E\d{4})\s*\|\s*(.+?)\s*\|\s*(.+?)\s*\|\s*(.+?)\s*\|\s*$");

        foreach (var line in File.ReadLines(_registryPath)) {
            var m = rowPattern.Match(line);
            if (!m.Success) continue;
            var code = m.Groups[1].Value;
            if (!seen.Add(code)) continue; // first occurrence (summary index) wins
            rows.Add(new RegistryRow(
                code,
                m.Groups[2].Value.Trim(),
                m.Groups[3].Value.Trim(),
                m.Groups[4].Value.Trim()));
        }

        return rows;
    }

    [Fact]
    public void Catalog_CoversEveryRegisteredCode() {
        var registry = ParseSummaryIndex().Select(r => r.Code).ToHashSet();
        var catalog = ErrorCatalog.All.Select(d => d.Code).ToHashSet();

        var missing = registry.Except(catalog).OrderBy(c => c).ToList();
        Assert.True(missing.Count == 0,
            $"Codes in the registry but absent from ErrorCatalog: {string.Join(", ", missing)}");
    }

    [Fact]
    public void Catalog_HasNoUnregisteredDescriptors() {
        var registry = ParseSummaryIndex().Select(r => r.Code).ToHashSet();
        var catalog = ErrorCatalog.All.Select(d => d.Code).ToHashSet();

        var orphaned = catalog.Except(registry).OrderBy(c => c).ToList();
        Assert.True(orphaned.Count == 0,
            $"Descriptors in ErrorCatalog with no registry row: {string.Join(", ", orphaned)}");
    }

    [Fact]
    public void Titles_MatchTheRegistry() {
        var registry = ParseSummaryIndex().ToDictionary(r => r.Code, r => r.Title);
        var mismatches = new List<string>();

        foreach (var d in ErrorCatalog.All) {
            if (registry.TryGetValue(d.Code, out var title) && title != d.Title) {
                mismatches.Add($"{d.Code}: catalog \"{d.Title}\" != registry \"{title}\"");
            }
        }

        Assert.True(mismatches.Count == 0,
            $"Title drift between ErrorCatalog and the registry:{Environment.NewLine}" +
            string.Join(Environment.NewLine, mismatches));
    }

    [Fact]
    public void Codes_AreUniqueInTheCatalog() {
        var dupes = ErrorCatalog.All
            .GroupBy(d => d.Code)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(c => c)
            .ToList();

        Assert.True(dupes.Count == 0,
            $"Duplicate descriptors in ErrorCatalog: {string.Join(", ", dupes)}");
    }

    [Fact]
    public void ThrowsLeaf_PresentForRuntimeCodesOnly() {
        var violations = new List<string>();

        foreach (var d in ErrorCatalog.All) {
            var isRuntime = d.Category == ErrorCategory.Runtime;
            if (isRuntime && d.Throws is null) {
                violations.Add($"{d.Code} is Runtime but has no Throws leaf");
            } else if (!isRuntime && d.Throws is not null) {
                violations.Add($"{d.Code} is {d.Category} but declares Throws={d.Throws}");
            }
        }

        Assert.True(violations.Count == 0,
            $"Throws-leaf rule violations:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations));
    }

    private static string LocateRegistry(string fileName) {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            var candidate = Path.Combine(dir.FullName, "docs", "design", fileName);
            if (File.Exists(candidate)) return candidate;

            var atRoot = Path.Combine(dir.FullName, fileName);
            if (File.Exists(atRoot)) return atRoot;

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {fileName} by walking up from {AppContext.BaseDirectory}. " +
            "The agreement test needs the canonical registry on disk.");
    }
}
