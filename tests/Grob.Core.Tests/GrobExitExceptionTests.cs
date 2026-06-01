using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public sealed class GrobExitExceptionTests {
    [Fact]
    public void Constructor_SetsCodeAndMessage() {
        var ex = new GrobExitException(42);

        Assert.Equal(42, ex.Code);
        Assert.Equal("exit(42)", ex.Message);
    }

    [Fact]
    public void Constructor_ZeroCode_IsValid() {
        var ex = new GrobExitException(0);

        Assert.Equal(0, ex.Code);
        Assert.Equal("exit(0)", ex.Message);
    }
}
