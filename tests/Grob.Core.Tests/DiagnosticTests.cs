using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public sealed class DiagnosticTests {
    private static readonly SourceRange _range = new(
        new SourceLocation("test.grob", 14, 7),
        new SourceLocation("test.grob", 14, 19));

    // Construction and properties

    [Fact]
    public void Constructor_ValidArguments_SetsAllProperties() {
        var diagnostic = new Diagnostic("E0001", "type mismatch", _range, Severity.Error);

        Assert.Equal("E0001", diagnostic.Code);
        Assert.Equal("type mismatch", diagnostic.Message);
        Assert.Equal(_range, diagnostic.Range);
        Assert.Equal(Severity.Error, diagnostic.Severity);
    }

    // Validation

    [Fact]
    public void Constructor_NullCode_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => new Diagnostic(null!, "message", _range, Severity.Error));
    }

    [Fact]
    public void Constructor_EmptyCode_ThrowsArgumentException() {
        Assert.Throws<ArgumentException>(() => new Diagnostic("", "message", _range, Severity.Error));
    }

    [Fact]
    public void Constructor_NullMessage_ThrowsArgumentNullException() {
        Assert.Throws<ArgumentNullException>(() => new Diagnostic("E0001", null!, _range, Severity.Error));
    }

    [Fact]
    public void Constructor_EmptyMessage_Succeeds() {
        var diagnostic = new Diagnostic("E0001", "", _range, Severity.Error);

        Assert.Equal("", diagnostic.Message);
    }

    // Equality

    [Fact]
    public void Equals_IdenticalFields_ReturnsTrue() {
        var a = new Diagnostic("E0001", "type mismatch", _range, Severity.Error);
        var b = new Diagnostic("E0001", "type mismatch", _range, Severity.Error);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferingField_ReturnsFalse() {
        var a = new Diagnostic("E0001", "type mismatch", _range, Severity.Error);
        var b = new Diagnostic("E0002", "type mismatch", _range, Severity.Error);

        Assert.NotEqual(a, b);
    }

    // ToString

    [Fact]
    public void ToString_ProducesRustcStyleFormat() {
        var diagnostic = new Diagnostic("E0001", "type mismatch", _range, Severity.Error);

        Assert.Equal("error[E0001]: type mismatch\n  --> test.grob:14:7-14:19", diagnostic.ToString());
    }
}
