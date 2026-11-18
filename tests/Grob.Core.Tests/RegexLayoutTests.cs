using System.Text.RegularExpressions;

using Xunit;

namespace Grob.Core.Tests;

/// <summary>
/// <see cref="RegexLayout"/> is the one definition of a <c>Regex</c> and a <c>Match</c>
/// value that the type checker, the compiler and <c>Grob.Stdlib</c>'s <c>RegexPlugin</c>
/// all read (D-432, D-440). The checker validates a pattern with the same options the
/// plugin runs it with, so flag, option and timeout rules live here once.
/// </summary>
public sealed class RegexLayoutTests {
    [Fact]
    public void TypeNames_AreRegexAndMatch() {
        Assert.Equal("Regex", RegexLayout.RegexTypeName);
        Assert.Equal("Match", RegexLayout.MatchTypeName);
    }

    [Fact]
    public void FieldNames_AreTheUnspellableHiddenFields() {
        Assert.Equal("__pattern", RegexLayout.PatternFieldName);
        Assert.Equal("__flags", RegexLayout.FlagsFieldName);
        Assert.Equal("__timeout", RegexLayout.TimeoutFieldName);
        Assert.Equal("__value", RegexLayout.ValueFieldName);
        Assert.Equal("__index", RegexLayout.IndexFieldName);
        Assert.Equal("__length", RegexLayout.LengthFieldName);
        Assert.Equal("__groups", RegexLayout.GroupsFieldName);
        Assert.Equal("__named", RegexLayout.NamedGroupsFieldName);
    }

    [Fact]
    public void DefaultTimeoutSeconds_IsFive() => Assert.Equal(5, RegexLayout.DefaultTimeoutSeconds);

    [Theory]
    [InlineData("", "")]
    [InlineData("i", "i")]
    [InlineData("m", "m")]
    [InlineData("im", "im")]
    [InlineData("mi", "im")]
    [InlineData("iim", "im")]
    [InlineData("mm", "m")]
    public void TryNormaliseFlags_ValidFlags_DeduplicatesInCanonicalOrder(string flags, string expected) {
        Assert.True(RegexLayout.TryNormaliseFlags(flags, out string normalised));
        Assert.Equal(expected, normalised);
    }

    [Theory]
    [InlineData("g")]
    [InlineData("I")]
    [InlineData("is")]
    [InlineData(" ")]
    public void TryNormaliseFlags_UnknownFlag_Fails(string flags) =>
        Assert.False(RegexLayout.TryNormaliseFlags(flags, out _));

    [Theory]
    [InlineData("", RegexOptions.CultureInvariant)]
    [InlineData("i", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    [InlineData("m", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    [InlineData("im", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    public void ToOptions_AlwaysCultureInvariant(string flags, RegexOptions expected) =>
        Assert.Equal(expected, RegexLayout.ToOptions(flags));

    [Fact]
    public void TryToTimeout_PositiveSeconds_IsThatManySeconds() {
        Assert.True(RegexLayout.TryToTimeout(5, out TimeSpan timeout));
        Assert.Equal(TimeSpan.FromSeconds(5), timeout);
    }

    [Fact]
    public void TryToTimeout_Zero_IsNoLimit() {
        Assert.True(RegexLayout.TryToTimeout(0, out TimeSpan timeout));
        Assert.Equal(Regex.InfiniteMatchTimeout, timeout);
    }

    [Fact]
    public void TryToTimeout_Negative_Fails() => Assert.False(RegexLayout.TryToTimeout(-1, out _));

    [Fact]
    public void TryToTimeout_TheCeiling_IsAcceptedByDotNet() {
        Assert.True(RegexLayout.TryToTimeout(RegexLayout.MaxTimeoutSeconds, out TimeSpan timeout));
        Regex regex = new("a", RegexOptions.None, timeout);
        Assert.Equal(timeout, regex.MatchTimeout);
    }

    [Fact]
    public void TryToTimeout_AboveTheCeiling_Fails() =>
        Assert.False(RegexLayout.TryToTimeout(RegexLayout.MaxTimeoutSeconds + 1, out _));

    [Fact]
    public void DescribeParseError_LeavesThePatternOut() {
        RegexParseException ex = Assert.Throws<RegexParseException>(() => new Regex("secret(value"));

        string description = RegexLayout.DescribeParseError(ex);

        Assert.DoesNotContain("secret", description);
        Assert.DoesNotContain("Invalid pattern", description);
        Assert.False(string.IsNullOrWhiteSpace(description));
    }

    [Fact]
    public void NewRegex_BuildsTheHiddenFieldStruct() {
        GrobStruct regex = RegexLayout.NewRegex(@"\d+", "im", 30);

        Assert.Equal("Regex", regex.TypeName);
        Assert.Equal(@"\d+", regex.GetField("__pattern").AsString());
        Assert.Equal("im", regex.GetField("__flags").AsString());
        Assert.Equal(30, regex.GetField("__timeout").AsInt());
        Assert.Equal(3, regex.Fields.Count);
    }
}
