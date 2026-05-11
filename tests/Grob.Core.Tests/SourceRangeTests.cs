using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public sealed class SourceRangeTests {
    private static readonly SourceLocation _a1 = new("src/foo.grob", 2, 3);
    private static readonly SourceLocation _a2 = new("src/foo.grob", 2, 9);
    private static readonly SourceLocation _b1 = new("src/bar.grob", 2, 3);

    // Construction and properties

    [Fact]
    public void Constructor_SameFile_EndAfterStart_SetsProperties() {
        var range = new SourceRange(_a1, _a2);

        Assert.Equal(_a1, range.Start);
        Assert.Equal(_a2, range.End);
    }

    // Validation

    [Fact]
    public void Constructor_DifferentFiles_ThrowsArgumentException() {
        Assert.Throws<ArgumentException>(() => new SourceRange(_a1, _b1));
    }

    [Fact]
    public void Constructor_EndOnEarlierLine_ThrowsArgumentException() {
        var start = new SourceLocation("src/foo.grob", 5, 1);
        var end = new SourceLocation("src/foo.grob", 4, 1);

        Assert.Throws<ArgumentException>(() => new SourceRange(start, end));
    }

    [Fact]
    public void Constructor_EndOnSameLineEarlierColumn_ThrowsArgumentException() {
        var start = new SourceLocation("src/foo.grob", 3, 8);
        var end = new SourceLocation("src/foo.grob", 3, 5);

        Assert.Throws<ArgumentException>(() => new SourceRange(start, end));
    }

    [Fact]
    public void Constructor_StartEqualsEnd_Succeeds() {
        var range = new SourceRange(_a1, _a1);

        Assert.Equal(_a1, range.Start);
        Assert.Equal(_a1, range.End);
    }

    // Convenience constructor

    [Fact]
    public void PointConstructor_ProducesStartEqualToEnd() {
        var range = new SourceRange(_a1);

        Assert.Equal(_a1, range.Start);
        Assert.Equal(_a1, range.End);
    }

    // Unknown sentinel

    [Fact]
    public void Unknown_HasStartAndEndEqualToSourceLocationUnknown() {
        Assert.Equal(SourceLocation.Unknown, SourceRange.Unknown.Start);
        Assert.Equal(SourceLocation.Unknown, SourceRange.Unknown.End);
    }

    // Equality and hash code

    [Fact]
    public void Equals_SameStartAndEnd_ReturnsTrue() {
        var a = new SourceRange(_a1, _a2);
        var b = new SourceRange(_a1, _a2);

        Assert.Equal(a, b);
    }

    [Fact]
    public void GetHashCode_EqualRanges_ReturnSameValue() {
        var a = new SourceRange(_a1, _a2);
        var b = new SourceRange(_a1, _a2);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentStart_ReturnsFalse() {
        var start2 = new SourceLocation("src/foo.grob", 1, 1);
        var a = new SourceRange(start2, _a2);
        var b = new SourceRange(_a1, _a2);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_DifferentEnd_ReturnsFalse() {
        var end2 = new SourceLocation("src/foo.grob", 3, 1);
        var a = new SourceRange(_a1, _a2);
        var b = new SourceRange(_a1, end2);

        Assert.NotEqual(a, b);
    }

    // ToString

    [Fact]
    public void ToString_ZeroWidth_ReturnsPointForm() {
        var range = new SourceRange(_a1);

        Assert.Equal("src/foo.grob:2:3", range.ToString());
    }

    [Fact]
    public void ToString_WiderRange_ReturnsSpanForm() {
        var range = new SourceRange(_a1, _a2);

        Assert.Equal("src/foo.grob:2:3-2:9", range.ToString());
    }
}
