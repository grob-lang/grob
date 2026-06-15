using Grob.BenchCheck;
using Xunit;

namespace Grob.BenchCheck.Tests;

public class BenchCheckTests
{
    private const string CompilePrefix = "Grob.Benchmarks.Compile";
    private const string Bench = "Grob.Benchmarks.Compile.CompileBenchmarks.Compile_TwoExpressions";

    private static readonly BdnHostEnvironmentInfo Windows =
        new("Windows 10 (10.0.20348.2461)", "Intel Xeon Platinum 8370C", "10.0.0");
    private static readonly BdnHostEnvironmentInfo Linux =
        new("Ubuntu 22.04.4 LTS", "AMD EPYC 7763", "10.0.0");

    private static Policy PolicyWith(bool compileGating) => new(
        PerSprintPercent: 5.0,
        CumulativePercent: 12.0,
        Categories: [new PolicyCategory("compile", CompilePrefix, "compile.json", compileGating)]);

    private static BaselineSide Side(BdnHostEnvironmentInfo host, double mean)
        => new(host, new Dictionary<string, double> { [Bench] = mean });

    private static Func<string, BaselineSide?> Loader(BaselineSide? rolling, BaselineSide? origin) => name => name switch
    {
        "compile.json" => rolling,
        "compile.origin.json" => origin,
        _ => null,
    };

    // --- arithmetic ---

    [Theory]
    [InlineData(110, 100, 10.0)]   // slower
    [InlineData(90, 100, -10.0)]   // faster
    [InlineData(100, 100, 0.0)]    // unchanged
    public void Percent_signs_a_regression_positive(double fresh, double baseline, double expected)
        => Assert.Equal(expected, BenchCheck.Percent(fresh, baseline), 3);

    [Fact]
    public void Percent_guards_zero_baseline()
        => Assert.Equal(0, BenchCheck.Percent(123, 0));

    [Fact]
    public void OriginName_inserts_origin_before_extension()
        => Assert.Equal("compile.origin.json", BenchCheck.OriginName("compile.json"));

    [Theory]
    [InlineData("Windows 10", "Windows Server 2022", true)]
    [InlineData("Windows 10", "Ubuntu 22.04", false)]
    public void SameRunnerType_compares_os_family_not_cpu(string a, string b, bool expected)
        => Assert.Equal(expected, BenchCheck.SameRunnerType(new(a, "cpu-x", "10"), new(b, "cpu-y", "10")));

    [Fact]
    public void SameRunnerType_unknown_never_matches()
        => Assert.False(BenchCheck.SameRunnerType(new("Plan 9", null, null), new("Plan 9", null, null)));

    // --- the gate ---

    [Fact]
    public void Within_both_thresholds_passes()
    {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(Windows, 102),                 // +2% vs rolling, +2% vs origin
            Loader(rolling: Side(Windows, 100), origin: Side(Windows, 100)));

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(DeltaClass.Ok, Assert.Single(report.Deltas).Class);
    }

    [Fact]
    public void Acute_per_sprint_regression_fails()
    {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(Windows, 130),                 // +30% vs rolling
            Loader(rolling: Side(Windows, 100), origin: Side(Windows, 100)));

        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(DeltaClass.PerSprintBreach, Assert.Single(report.Deltas).Class);
    }

    [Fact]
    public void Slow_creep_trips_cumulative_even_when_per_sprint_is_in_tolerance()
    {
        // The ratchet case D-313 exists to catch: each sprint adds a little,
        // each step is under 5% vs the prior baseline, but the total since
        // origin has crossed the 12% ceiling.
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(Windows, 113),                 // +2.7% vs rolling (110), +13% vs origin (100)
            Loader(rolling: Side(Windows, 110), origin: Side(Windows, 100)));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(DeltaClass.CumulativeBreach, delta.Class);
        Assert.True(delta.PerSprintPercent < 5.0);
        Assert.True(delta.CumulativePercent > 12.0);
    }

    [Fact]
    public void Non_gating_category_regression_is_reported_not_failed()
    {
        var policy = new Policy(5.0, 12.0,
            [new PolicyCategory("compile", CompilePrefix, "compile.json", Gating: false)]);

        var report = BenchCheck.Evaluate(
            policy,
            fresh: Side(Windows, 200),                 // +100%, but category is informational
            Loader(rolling: Side(Windows, 100), origin: Side(Windows, 100)));

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(DeltaClass.Informational, Assert.Single(report.Deltas).Class);
    }

    [Fact]
    public void Missing_rolling_baseline_is_establishing_not_a_failure()
    {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(Windows, 100),
            Loader(rolling: null, origin: null));

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(DeltaClass.NoBaseline, Assert.Single(report.Deltas).Class);
    }

    [Fact]
    public void Runner_mismatch_on_gating_category_cannot_compare()
    {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(Linux, 100),                   // ran on linux
            Loader(rolling: Side(Windows, 100), origin: Side(Windows, 100)));  // baseline on windows

        Assert.Equal(Outcome.CannotCompare, report.Outcome);
        Assert.Equal(DeltaClass.RunnerMismatch, Assert.Single(report.Deltas).Class);
    }

    [Fact]
    public void New_benchmark_absent_from_baseline_is_informational()
    {
        var fresh = new BaselineSide(Windows, new Dictionary<string, double>
        {
            [Bench] = 100,
            ["Grob.Benchmarks.Compile.CompileBenchmarks.Compile_BrandNew"] = 999,
        });
        var rolling = new BaselineSide(Windows, new Dictionary<string, double> { [Bench] = 100 });

        var report = BenchCheck.Evaluate(PolicyWith(compileGating: true), fresh, Loader(rolling, rolling));

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Contains(report.Deltas, d => d.Class == DeltaClass.NewBenchmark);
    }

    [Fact]
    public void Missing_origin_skips_cumulative_axis_without_failing()
    {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(Windows, 103),                 // +3% vs rolling, under the per-sprint gate
            Loader(rolling: Side(Windows, 100), origin: null));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Null(delta.CumulativePercent);
        Assert.Equal(DeltaClass.Ok, delta.Class);
    }
}
