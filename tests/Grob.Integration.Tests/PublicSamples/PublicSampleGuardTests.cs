using Grob.Stdlib;

using Xunit;

namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// The public samples gate's guards (D-430 Decisions 1, 3 and 8, ruling 6): each proven on a
/// planted input, then held against the live tree. The per-block corpus cases join in change
/// group 4.
/// </summary>
public sealed class PublicSampleGuardTests {
    [Fact]
    public void ExcusedCoreModule_RegisteredInTheCatalogue_IsReported() {
        Assert.Equal(
            ["namespace `json` is registered in the stdlib catalogue; remove it from the excused list"],
            ExcusedNamespaces.RegisteredCoreModules(ns => ns == "json"));
    }

    [Fact]
    public void ExcusedCoreModules_AreAbsentFromTheCatalogue() {
        Assert.Empty(ExcusedNamespaces.RegisteredCoreModules(StdlibCatalogue.ForCompileOnly().IsNamespace));
    }

    [Fact]
    public void UnreadFixtures_FixtureNoBlockNames_IsReported() {
        var fixtures = new Dictionary<string, string> { ["READ_ME"] = "a", ["UNUSED"] = "b" };
        SampleBlock[] blocks = [new("p.md", 1, null, "", "pat := env.require(\"READ_ME\")\n")];

        Assert.Equal(["UNUSED"], PublicSampleCorpus.UnreadFixtures(fixtures, blocks));
    }

    [Fact]
    public void FixtureEnvironment_EveryVariableIsReadByABlock() {
        Assert.Empty(PublicSampleCorpus.UnreadFixtures(PublicSampleCorpus.FixtureEnvironment, PublicSampleCorpus.Blocks()));
    }

    [Fact]
    public void Pages_IncludeTheReadmeAndExcludeTheAdrs() {
        IReadOnlyList<string> pages = PublicSampleCorpus.Pages();

        Assert.Equal("README.md", pages[0]);
        Assert.Contains(pages, p => p.StartsWith("docs/wiki/Standard-Library/", StringComparison.Ordinal));
        Assert.DoesNotContain(pages, p => p.StartsWith("docs/wiki/ADR/", StringComparison.Ordinal));
        Assert.NotEmpty(Directory.EnumerateFiles(
            Path.Join(PublicSampleCorpus.RepositoryRoot, "docs", "wiki", "ADR"), "*.md", SearchOption.AllDirectories));
    }

    [Fact]
    public void Corpus_HasNoNearMissFence() {
        string[] nearMisses = [.. PublicSampleCorpus.Pages()
            .SelectMany(p => SampleExtractor.NearMisses(p, PublicSampleCorpus.Read(p)))];

        Assert.True(nearMisses.Length == 0, string.Join("\n", nearMisses));
    }
}
