using Grob.Integration.Tests.ErrorExamples;

using Xunit;

namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// <see cref="InfoString"/> (gate report §1.5): the two tokens in either order and together, and
/// each rejection with its message.
/// </summary>
public sealed class InfoStringTests {
    private static readonly DeferredWorkRegister Register = new(
        new HashSet<string>(StringComparer.Ordinal) { "R-29", "R-30", "R-100" },
        new Dictionary<string, string>(StringComparer.Ordinal) { ["R-01"] = "D-123" });

    [Fact]
    public void Parse_Empty_IsABareBlock() {
        SampleMarkers markers = InfoString.Parse("", Register);

        Assert.False(markers.Fragment);
        Assert.Empty(markers.BlockedBy);
        Assert.Empty(markers.Problems);
    }

    [Theory]
    [InlineData("fragment blocked-by=R-30")]
    [InlineData("blocked-by=R-30 fragment")]
    [InlineData("blocked-by=R-30   fragment")]
    public void Parse_BothTokensInEitherOrder_AreRead(string info) {
        SampleMarkers markers = InfoString.Parse(info, Register);

        Assert.True(markers.Fragment);
        Assert.Equal(["R-30"], markers.BlockedBy);
        Assert.Empty(markers.Problems);
    }

    [Fact]
    public void Parse_TwoDistinctOpenRows_AreBothRead() {
        SampleMarkers markers = InfoString.Parse("blocked-by=R-29 blocked-by=R-100", Register);

        Assert.Equal(["R-29", "R-100"], markers.BlockedBy);
        Assert.Empty(markers.Problems);
    }

    [Theory]
    [InlineData("Fragment", "unknown info-string token 'Fragment'; a grob fence takes only 'fragment' and 'blocked-by=R-##'")]
    [InlineData("fragment fragment", "'fragment' appears more than once in the info string")]
    [InlineData("blocked-by=R-3", "'blocked-by=R-3' is not a blocker; write 'blocked-by=R-' followed by two or more digits")]
    [InlineData("blocked-by=X-30", "'blocked-by=X-30' is not a blocker; write 'blocked-by=R-' followed by two or more digits")]
    [InlineData("blocked-by=R-30 blocked-by=R-30", "'R-30' is named more than once in the info string")]
    [InlineData("blocked-by=R-77", "R-77 is not a row of the Deferred Work Register")]
    [InlineData("blocked-by=R-01", "R-01 is closed (by D-123); re-classify this block or remove the token")]
    public void Parse_MalformedInfoString_ReportsTheRejection(string info, string message) {
        Assert.Equal([message], InfoString.Parse(info, Register).Problems);
    }

    [Fact]
    public void DeferredWorkRegister_ThreeDigitRow_IsRead() {
        const string text = "### Open items\n\n| R-100 | text |\n\n### Closed items\n\n| R-101 | text | **D-500** |\n";

        DeferredWorkRegister register = DeferredWorkRegister.Parse(text);

        Assert.Contains("R-100", register.Open);
        Assert.Equal("D-500", register.ClosedBy["R-101"]);
    }
}
