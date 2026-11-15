using Grob.Integration.Tests.ErrorExamples;

using Xunit;

namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// The public samples gate over the live corpus (D-430): every <c>grob</c> block in
/// <c>README.md</c> and the wiki runs through <see cref="RunCommand"/>'s own pipeline and is judged
/// against the class its info string declares. One case per block, named by its display name, so a
/// failure names the page, the section and the mismatch.
/// </summary>
public sealed class PublicSampleCorpusTests {
    private static readonly Lazy<IReadOnlyDictionary<string, SampleBlock>> ByName = new(() =>
        PublicSampleCorpus.Blocks().ToDictionary(b => b.DisplayName, StringComparer.Ordinal));

    private static readonly Lazy<DeferredWorkRegister> Register = new(() =>
        DeferredWorkRegister.Parse(File.ReadAllText(ErrorExampleCorpus.OpenQuestionsPath)));

    public static TheoryData<string> Blocks => [.. PublicSampleCorpus.Blocks().Select(b => b.DisplayName)];

    /// <summary>
    /// Every block has a well-formed info string and passes its class, or carries a
    /// <c>blocked-by</c> token and fails it. A blocked block is run like any other, never skipped:
    /// a skip would hide a block that starts passing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Blocks))]
    public void Block_PassesItsClass(string block) {
        SampleBlock sample = ByName.Value[block];
        SampleMarkers markers = InfoString.Parse(sample.Info, Register.Value);
        Assert.True(markers.Problems.Count == 0, $"{block}:\n  {string.Join("\n  ", markers.Problems)}");

        string? failure = SampleVerdict.Check(markers, SampleRunner.Run(sample.Source, PublicSampleCorpus.FixtureEnvironment));
        Assert.True(failure is null, $"{block}: {failure}");
    }
}
