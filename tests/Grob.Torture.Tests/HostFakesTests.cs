using Xunit;

using Grob.Torture.Fakes;

namespace Grob.Torture.Tests;

/// <summary>
/// The oracle runs every program against fakes and touches nothing on the machine (prompt §1.4).
/// No native writes a file or runs a process before 9C, so the isolation shown here is the
/// surface that exists: the environment, the current directory, the clock and the streams
/// (gate report §4.2).
/// </summary>
public sealed class HostFakesTests {
    private const string ProbeVariable = "GROB_TORTURE_ISOLATION_PROBE";

    private static (OracleResult Result, HostFakes Fakes) RunCapturing(string source) {
        HostFakes? captured = null;
        var oracle = new Oracle(OracleBudget.Ci, fakes: () => captured = HostFakes.Create());
        OracleResult result = oracle.Run(source);
        return (result, captured!);
    }

    [Fact]
    public void Run_EnvSet_WritesTheFakeAndNotTheProcessEnvironment() {
        (OracleResult result, HostFakes fakes) = RunCapturing($"env.set(\"{ProbeVariable}\", \"x\")\n");

        Assert.Equal(OutcomeKind.Clean, result.Outcome);
        Assert.Equal("x", fakes.Environment.Get(ProbeVariable));
        Assert.Null(Environment.GetEnvironmentVariable(ProbeVariable));
    }

    [Fact]
    public void Run_EnvGet_ReadsOnlyTheFake() {
        (_, HostFakes fakes) = RunCapturing("print(env.has(\"PATH\"))\n");

        Assert.Equal("false", fakes.Streams.OutText.Trim());
    }

    [Fact]
    public void Run_PathResolve_AnchorsToTheFakeDirectory() {
        (_, HostFakes fakes) = RunCapturing("print(path.resolve(\"a\"))\n");

        Assert.Equal(Path.Join(FixedFileSystem.Root, "a"), fakes.Streams.OutText.Trim());
    }

    [Fact]
    public void Run_DateNow_ReadsTheFixedClock() {
        (_, HostFakes fakes) = RunCapturing("print(date.now().year)\n");

        Assert.Equal("2026", fakes.Streams.OutText.Trim());
    }

    [Fact]
    public void Run_PrintAndInput_UseOnlyTheFakeStreams() {
        (OracleResult result, HostFakes fakes) = RunCapturing("print(\"out\")\nline := input()\n");

        Assert.Equal("out", fakes.Streams.OutText.Trim());
        Assert.Equal(OutcomeKind.RuntimeError, result.Outcome);
    }

    [Fact]
    public void Run_LogError_WritesOnlyTheFakeErrorStream() {
        (_, HostFakes fakes) = RunCapturing("log.error(\"bad\")\n");

        Assert.Contains("bad", fakes.Streams.ErrorText, StringComparison.Ordinal);
        Assert.Empty(fakes.Streams.OutText);
    }

    [Fact]
    public void Create_EachCall_GivesAFreshEnvironment() {
        HostFakes first = HostFakes.Create();
        first.Environment.Set("A", "1");

        Assert.False(HostFakes.Create().Environment.Has("A"));
        Assert.Equal(["A"], first.Environment.All().Keys);
    }

    [Fact]
    public void SplitMix_SameSeed_GivesTheSameSequence() {
        var a = new SplitMixRandomSource(7);
        var b = new SplitMixRandomSource(7);

        Assert.Equal(
            [a.NextBits(), a.NextBits(), a.NextBits()],
            [b.NextBits(), b.NextBits(), b.NextBits()]);
    }

    [Fact]
    public void SplitMix_Reseed_RestartsTheSequence() {
        var source = new SplitMixRandomSource(7);
        double first = source.NextDouble();
        source.NextDouble();

        source.Reseed(7);

        Assert.Equal(first, source.NextDouble());
    }

    [Theory]
    [InlineData(1, 6)]
    [InlineData(-3, 3)]
    [InlineData(5, 5)]
    public void SplitMix_NextInt_StaysInsideInclusiveBounds(long min, long max) {
        var source = new SplitMixRandomSource(11);

        for (int i = 0; i < 200; i++) {
            long value = source.NextInt(min, max);
            Assert.InRange(value, min, max);
        }
    }

    [Fact]
    public void SplitMix_NextIntFullRange_DoesNotThrow() {
        var source = new SplitMixRandomSource(11);

        source.NextInt(long.MinValue, long.MaxValue);
    }

    [Fact]
    public void SplitMix_NextDouble_IsInUnitInterval() {
        var source = new SplitMixRandomSource(3);

        for (int i = 0; i < 200; i++) {
            Assert.InRange(source.NextDouble(), 0.0, 0.9999999999999999);
        }
    }

    [Fact]
    public void FixedClock_ReadsUtc() {
        var clock = new FixedClock();

        Assert.Equal(FixedClock.Instant, clock.UtcNow);
        Assert.Same(TimeZoneInfo.Utc, clock.LocalZone);
    }
}
