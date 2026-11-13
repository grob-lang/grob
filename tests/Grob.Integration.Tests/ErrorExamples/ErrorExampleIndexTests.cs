using Xunit;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The README's code-to-slug index against the pair directories (D-428): a bijection, with each
/// row's code agreeing with its master's first header.
/// </summary>
public sealed class ErrorExampleIndexTests {
    private static string Readme(params string[] rows) =>
        "# Examples\n\n-----\n\n## Code → slug index\n\n| Code | Slug | Category |\n|------|------|----------|\n" +
        string.Concat(rows.Select(r => r + "\n")) +
        "\n-----\n\n*footer*\n";

    private static Dictionary<string, string?> Directories(params (string Slug, string? Code)[] pairs) =>
        pairs.ToDictionary(p => p.Slug, p => p.Code, StringComparer.Ordinal);

    [Fact]
    public void CheckIndex_Bijection_HasNoProblems() {
        string readme = Readme("| E0001 | `a` | Type |", "| E0002 | `b` | Type |");

        Assert.Empty(ErrorExampleStaticChecks.CheckIndex(readme, Directories(("a", "E0001"), ("b", "E0002"))));
    }

    [Fact]
    public void CheckIndex_RowWithNoDirectory_Fails() {
        string readme = Readme("| E0001 | `a` | Type |", "| E0002 | `gone` | Type |");

        string problem = Assert.Single(ErrorExampleStaticChecks.CheckIndex(readme, Directories(("a", "E0001"))));
        Assert.Equal("index row `gone` has no directory", problem);
    }

    [Fact]
    public void CheckIndex_DirectoryWithNoRow_Fails() {
        string readme = Readme("| E0001 | `a` | Type |");

        string problem = Assert.Single(ErrorExampleStaticChecks.CheckIndex(readme,
            Directories(("a", "E0001"), ("unindexed", "E0002"))));
        Assert.Equal("directory `unindexed` has no index row", problem);
    }

    [Fact]
    public void CheckIndex_DuplicateRow_Fails() {
        string readme = Readme("| E0001 | `a` | Type |", "| E0001 | `a` | Type |");

        string problem = Assert.Single(ErrorExampleStaticChecks.CheckIndex(readme, Directories(("a", "E0001"))));
        Assert.Equal("index lists `a` twice", problem);
    }

    [Fact]
    public void CheckIndex_CodeDisagreesWithMaster_Fails() {
        string readme = Readme("| E0001 | `a` | Type |");

        string problem = Assert.Single(ErrorExampleStaticChecks.CheckIndex(readme, Directories(("a", "E0002"))));
        Assert.Equal("index gives `a` E0001; its master's first header is E0002", problem);
    }

    [Fact]
    public void CheckIndex_NoIndexSection_Fails() {
        string problem = Assert.Single(ErrorExampleStaticChecks.CheckIndex("# Examples\n", Directories(("a", "E0001"))));
        Assert.Equal("README has no Code → slug index section", problem);
    }

    [Fact]
    public void CheckIndex_CrLfReadme_ReadsRows() {
        string readme = Readme("| E0001 | `a` | Type |").Replace("\n", "\r\n");

        Assert.Empty(ErrorExampleStaticChecks.CheckIndex(readme, Directories(("a", "E0001"))));
    }
}
