using Xunit;

namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// <see cref="SampleExtractor"/> over inline markdown (D-430 Decision 1, gate report §1.5): what
/// counts as a <c>grob</c> block, its identity and display name, and ruling 6's near-miss guard,
/// one test per shape.
/// </summary>
public sealed class SampleExtractorTests {
    private const string Page = "docs/wiki/Page.md";

    [Fact]
    public void Extract_TwoBlocksOnOnePage_EachCarriesItsLineAndNearestHeading() {
        const string text = "# Title\n\n## First\n\n```grob\nprint(1)\n```\n\n## Second\n\n```grob\nprint(2)\nprint(3)\n```\n";

        IReadOnlyList<SampleBlock> blocks = SampleExtractor.Extract(Page, text);

        Assert.Equal(2, blocks.Count);
        Assert.Equal(new SampleBlock(Page, 5, "First", "", "print(1)\n"), blocks[0]);
        Assert.Equal(new SampleBlock(Page, 11, "Second", "", "print(2)\nprint(3)\n"), blocks[1]);
        Assert.Equal("docs/wiki/Page.md:11 (Second)", blocks[1].DisplayName);
    }

    [Fact]
    public void Extract_BlockAtEndOfFileWithNoClosingFence_RunsToTheEnd() {
        IReadOnlyList<SampleBlock> blocks = SampleExtractor.Extract(Page, "```grob\nprint(1)\nprint(2)");

        SampleBlock block = Assert.Single(blocks);
        Assert.Equal("print(1)\nprint(2)\n", block.Source);
        Assert.Equal("docs/wiki/Page.md:1", block.DisplayName);
    }

    [Fact]
    public void Extract_CrlfFile_ReadsTheBlockWithoutCarriageReturns() {
        IReadOnlyList<SampleBlock> blocks = SampleExtractor.Extract(Page, "## H\r\n\r\n```grob\r\nprint(1)\r\n```\r\n");

        SampleBlock block = Assert.Single(blocks);
        Assert.Equal(3, block.Line);
        Assert.Equal("H", block.Heading);
        Assert.Equal("print(1)\n", block.Source);
    }

    [Fact]
    public void Extract_InfoString_IsWhatFollowsGrobWithTrailingSpaceTrimmed() {
        SampleBlock block = Assert.Single(SampleExtractor.Extract(Page, "```grob fragment blocked-by=R-30  \nx\n```\n"));

        Assert.Equal("fragment blocked-by=R-30", block.Info);
    }

    [Theory]
    [InlineData("  ```grob\n  print(1)\n  ```\n")]
    [InlineData("```Grob\nprint(1)\n```\n")]
    [InlineData("```grob-x\nprint(1)\n```\n")]
    [InlineData("```grobx\nprint(1)\n```\n")]
    [InlineData("~~~grob\nprint(1)\n~~~\n")]
    [InlineData("````grob\nprint(1)\n````\n")]
    public void Extract_NonCanonicalFence_IsNotExtracted(string text) {
        Assert.Empty(SampleExtractor.Extract(Page, text));
    }

    [Fact]
    public void Extract_GrobFenceInsideAnotherFence_IsNotExtracted() {
        const string text = "````markdown\n```grob\nprint(1)\n```\n````\n\n```grob\nprint(2)\n```\n";

        SampleBlock block = Assert.Single(SampleExtractor.Extract(Page, text));
        Assert.Equal("print(2)\n", block.Source);
    }

    [Theory]
    [InlineData("  ```grob\nx\n  ```\n")]
    [InlineData("```Grob\nx\n```\n")]
    [InlineData("```GROB fragment\nx\n```\n")]
    [InlineData("~~~grob\nx\n~~~\n")]
    [InlineData("````grob\nx\n````\n")]
    [InlineData("```grob-fragment\nx\n```\n")]
    [InlineData("```grob,x\nx\n```\n")]
    public void NearMisses_GrobLikeFenceNotExtracted_IsReported(string text) {
        string nearMiss = Assert.Single(SampleExtractor.NearMisses(Page, text));

        Assert.StartsWith("docs/wiki/Page.md:1 opens a grob-like fence", nearMiss);
    }

    [Theory]
    [InlineData("```grob\nx\n```\n")]
    [InlineData("```grob fragment\nx\n```\n")]
    [InlineData("```grobparams\nx\n```\n")]
    [InlineData("```text\ngrob\n```\n")]
    [InlineData("```\nx\n```\n")]
    public void NearMisses_CanonicalOrUnrelatedFence_IsNotReported(string text) {
        Assert.Empty(SampleExtractor.NearMisses(Page, text));
    }
}
