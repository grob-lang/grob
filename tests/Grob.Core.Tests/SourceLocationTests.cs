using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public sealed class SourceLocationTests {
    // Construction and properties

    [Fact]
    public void Constructor_ValidArguments_SetsAllProperties() {
        var location = new SourceLocation("src/foo.grob", 3, 7);

        Assert.Equal("src/foo.grob", location.File);
        Assert.Equal(3, location.Line);
        Assert.Equal(7, location.Column);
    }

    // Validation

    [Fact]
    public void Constructor_NullFile_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => new SourceLocation(null!, 1, 1));
    }

    [Fact]
    public void Constructor_LineZero_ThrowsArgumentOutOfRangeException() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceLocation("file.grob", 0, 1));
    }

    [Fact]
    public void Constructor_LineNegative_ThrowsArgumentOutOfRangeException() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceLocation("file.grob", -1, 1));
    }

    [Fact]
    public void Constructor_ColumnZero_ThrowsArgumentOutOfRangeException() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceLocation("file.grob", 1, 0));
    }

    [Fact]
    public void Constructor_ColumnNegative_ThrowsArgumentOutOfRangeException() {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceLocation("file.grob", 1, -1));
    }

    // Equality

    [Fact]
    public void Equals_SameFileLineColumn_ReturnsTrue() {
        var a = new SourceLocation("src/foo.grob", 2, 5);
        var b = new SourceLocation("src/foo.grob", 2, 5);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentFile_ReturnsFalse() {
        var a = new SourceLocation("src/foo.grob", 2, 5);
        var b = new SourceLocation("src/bar.grob", 2, 5);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_DifferentLine_ReturnsFalse() {
        var a = new SourceLocation("src/foo.grob", 2, 5);
        var b = new SourceLocation("src/foo.grob", 3, 5);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_DifferentColumn_ReturnsFalse() {
        var a = new SourceLocation("src/foo.grob", 2, 5);
        var b = new SourceLocation("src/foo.grob", 2, 6);

        Assert.NotEqual(a, b);
    }

    // Hash code

    [Fact]
    public void GetHashCode_EqualLocations_ReturnSameHashCode() {
        var a = new SourceLocation("src/foo.grob", 2, 5);
        var b = new SourceLocation("src/foo.grob", 2, 5);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    // ToString

    [Fact]
    public void ToString_ReturnsFileLineColumnForm() {
        var location = new SourceLocation("src/foo.grob", 3, 7);

        Assert.Equal("src/foo.grob:3:7", location.ToString());
    }

    // Unknown sentinel

    [Fact]
    public void Unknown_IsNotEqualToConstructedLocationWithDifferentFile() {
        var other = new SourceLocation("src/foo.grob", 1, 1);

        Assert.NotEqual(SourceLocation.Unknown, other);
    }

    // Operators

    [Fact]
    public void EqualityOperator_EqualLocations_ReturnsTrue() {
        var a = new SourceLocation("src/foo.grob", 2, 5);
        var b = new SourceLocation("src/foo.grob", 2, 5);

        Assert.True(a == b);
    }

    [Fact]
    public void InequalityOperator_DifferentLocations_ReturnsTrue() {
        var a = new SourceLocation("src/foo.grob", 2, 5);
        var b = new SourceLocation("src/foo.grob", 2, 6);

        Assert.True(a != b);
    }
}
