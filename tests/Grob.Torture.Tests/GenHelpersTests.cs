using Grob.Torture.Generation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>The generator's random choices and its writer.</summary>
public sealed class GenHelpersTests {
    [Fact]
    public void Weighted_AllWeightsZero_Throws() {
        var random = new GenRandom(1);

        Assert.Throws<InvalidOperationException>(() => random.Weighted<int>([(0, 1), (0, 2)]));
    }

    [Fact]
    public void Weighted_OneNonZeroWeight_AlwaysPicksIt() {
        var random = new GenRandom(7);

        Assert.All(Enumerable.Range(0, 50), _ => Assert.Equal("b", random.Weighted<string>([(0, "a"), (3, "b"), (0, "c")])));
    }

    [Fact]
    public void Between_StaysWithinBothBounds() {
        var random = new GenRandom(3);

        List<int> draws = [.. Enumerable.Range(0, 500).Select(_ => random.Between(2, 4))];

        Assert.Equal([2, 3, 4], draws.Distinct().Order());
    }

    [Fact]
    public void Chance_ZeroAndAHundred_AreNeverAndAlways() {
        var random = new GenRandom(5);

        Assert.All(Enumerable.Range(0, 50), _ => Assert.False(random.Chance(0)));
        Assert.All(Enumerable.Range(0, 50), _ => Assert.True(random.Chance(100)));
    }

    [Fact]
    public void Writer_NestedBlocks_IndentByFourSpacesWithLineFeeds() {
        var writer = new CodeWriter();
        writer.Open("if (a) {");
        writer.Line("x := 1");
        writer.Reopen("} else {");
        writer.Line("x := 2");
        writer.Close();

        Assert.Equal("if (a) {\n    x := 1\n} else {\n    x := 2\n}\n", writer.ToString());
    }
}
