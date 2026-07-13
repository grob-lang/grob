using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// <see cref="TestRandomSource"/> mirrors production <c>SystemRandomSource</c>'s
/// <c>NextInt</c> shape, so it carries the same overflow fix (CodeRabbit review, PR
/// #130): <c>NextInt(min, long.MaxValue)</c> must not compute <c>max + 1</c>.
/// </summary>
public sealed class TestRandomSourceTests {
    [Fact]
    public void NextInt_MaxIsLongMaxValue_DoesNotThrow_AndStaysInRange() {
        var source = new TestRandomSource(1);

        for (int i = 0; i < 100; i++) {
            long value = source.NextInt(1, long.MaxValue);
            Assert.True(value >= 1, $"below minimum: {value}");
        }
    }

    [Fact]
    public void NextInt_MinIsLongMinValueAndMaxIsLongMaxValue_DoesNotThrow() {
        var source = new TestRandomSource(1);

        var ex = Record.Exception(() => source.NextInt(long.MinValue, long.MaxValue));

        Assert.Null(ex);
    }
}
