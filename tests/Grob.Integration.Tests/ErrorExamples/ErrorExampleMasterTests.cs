using Grob.Core;

using Xunit;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The static layer over the live corpus (D-428): one case per pair, named by its slug, plus the
/// README index bijection. These checks are never quarantinable; a failure is a defect in the pair.
/// </summary>
public sealed class ErrorExampleMasterTests {
    private static readonly IReadOnlyDictionary<string, string> CatalogueTitles =
        ErrorCatalog.All.ToDictionary(d => d.Code, d => d.Title, StringComparer.Ordinal);

    public static TheoryData<string> Pairs => [.. ErrorExampleCorpus.Slugs()];

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Master_IsConsistentWithSourceAndCatalogue(string slug) {
        IReadOnlyList<string> problems = ErrorExampleStaticChecks.CheckPair(slug,
            File.ReadAllText(ErrorExampleCorpus.SourcePath(slug)),
            File.ReadAllText(ErrorExampleCorpus.MasterPath(slug)),
            CatalogueTitles);

        Assert.True(problems.Count == 0, $"{slug}:\n  {string.Join("\n  ", problems)}");
    }

    [Fact]
    public void ReadmeIndex_IsBijectionWithPairDirectories() {
        Dictionary<string, string?> firstCodes = ErrorExampleCorpus.Slugs().ToDictionary(
            slug => slug,
            slug => ErrorExampleMaster.Parse(File.ReadAllText(ErrorExampleCorpus.MasterPath(slug)))
                .Diagnostics.FirstOrDefault()?.Code,
            StringComparer.Ordinal);

        IReadOnlyList<string> problems =
            ErrorExampleStaticChecks.CheckIndex(File.ReadAllText(ErrorExampleCorpus.ReadmePath), firstCodes);

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Corpus_HasFiftySixPairs() {
        Assert.Equal(56, ErrorExampleCorpus.Slugs().Count);
    }
}
