using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Direct tests for <c>Grob.Cli</c>'s <c>IRandomSource</c> implementation (Sprint 8
/// Increment B, D-343) — reachable here via the existing <c>Grob.Cli</c>
/// <c>InternalsVisibleTo</c> grant. Covers the boundary CodeRabbit flagged on PR #130:
/// <c>NextInt(min, long.MaxValue)</c> must not compute <c>max + 1</c> (which overflows
/// and makes .NET's <c>NextInt64</c> throw <c>ArgumentOutOfRangeException</c> on a
/// reachable script input, e.g. <c>math.randomInt(1, 9223372036854775807)</c>).
/// </summary>
public sealed class SystemRandomSourceTests {
    [Fact]
    public void NextInt_MaxIsLongMaxValue_DoesNotThrow_AndStaysInRange() {
        var source = new SystemRandomSource();

        for (int i = 0; i < 100; i++) {
            long value = source.NextInt(1, long.MaxValue);
            Assert.True(value >= 1, $"below minimum: {value}");
        }
    }

    [Fact]
    public void NextInt_MinIsLongMinValueAndMaxIsLongMaxValue_DoesNotThrow() {
        var source = new SystemRandomSource();

        var ex = Record.Exception(() => source.NextInt(long.MinValue, long.MaxValue));

        Assert.Null(ex);
    }

    [Fact]
    public void NextInt_NormalRange_StillInclusiveBothEnds() {
        var source = new SystemRandomSource();
        bool sawMin = false;
        bool sawMax = false;

        for (int i = 0; i < 500; i++) {
            long value = source.NextInt(1, 6);
            Assert.True(value is >= 1 and <= 6, $"out of range: {value}");
            if (value == 1) sawMin = true;
            if (value == 6) sawMax = true;
        }

        Assert.True(sawMin, "never drew the minimum over 500 draws");
        Assert.True(sawMax, "never drew the maximum over 500 draws");
    }

    [Fact]
    public void Reseed_SameSeedTwice_ProducesSameSequence() {
        var a = new SystemRandomSource();
        var b = new SystemRandomSource();

        a.Reseed(42);
        b.Reseed(42);

        Assert.Equal(a.NextDouble(), b.NextDouble());
        Assert.Equal(a.NextInt(1, 1000), b.NextInt(1, 1000));
    }
}
