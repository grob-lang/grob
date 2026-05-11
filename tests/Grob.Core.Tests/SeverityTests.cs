using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public sealed class SeverityTests {
    [Fact]
    public void Error_HasNumericValueOne() {
        Assert.Equal(1, (int)Severity.Error);
    }

    [Fact]
    public void Warning_HasNumericValueTwo() {
        Assert.Equal(2, (int)Severity.Warning);
    }

    [Fact]
    public void Info_HasNumericValueThree() {
        Assert.Equal(3, (int)Severity.Info);
    }

    [Fact]
    public void Hint_HasNumericValueFour() {
        Assert.Equal(4, (int)Severity.Hint);
    }

    [Fact]
    public void Error_ToStringReturnsError() {
        Assert.Equal("Error", Severity.Error.ToString());
    }
}
