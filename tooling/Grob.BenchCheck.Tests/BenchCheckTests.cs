using Grob.BenchCheck;
using Xunit;

namespace Grob.BenchCheck.Tests;

public class BenchCheckTests {
    private const string CompilePrefix = "Grob.Benchmarks.Compile";
    private const string Bench = "Grob.Benchmarks.Compile.CompileBenchmarks.Compile_TwoExpressions";

    private static readonly BdnHostEnvironmentInfo _epyc =
        new("Windows 11 (10.0.26100)", "AMD EPYC 7763", "10.0.9");
    private static readonly BdnHostEnvironmentInfo _xeon =
        new("Windows 11 (10.0.26100)", "Intel Xeon Platinum 8370C", "10.0.9");
    private static readonly BdnHostEnvironmentInfo _unknownCpu =
        new("Windows 11 (10.0.26100)", "Unknown processor", "10.0.9");

    private static Policy PolicyWith(bool compileGating, double allocPercent = 10.0, double lohTripwireBytes = 85000, double k = 3.0) => new(
        PerSprintPercent: 5.0,
        CumulativePercent: 12.0,
        AllocPercent: allocPercent,
        LohTripwireBytes: lohTripwireBytes,
        TimeSignificanceK: k,
        Categories: [new PolicyCategory("compile", CompilePrefix, "compile.json", compileGating)]);

    private static BenchmarkMeasurement M(double mean, double stdDev = 0, double? bytes = null) => new(mean, stdDev, bytes);

    private static BaselineSide Side(BdnHostEnvironmentInfo? host, BenchmarkMeasurement measurement, string name = Bench)
        => new(host, new Dictionary<string, BenchmarkMeasurement> { [name] = measurement });

    private static Func<string, BaselineSide?> Loader(BaselineSide? rolling, BaselineSide? origin) => name => name switch {
        "compile.json" => rolling,
        "compile.origin.json" => origin,
        _ => null,
    };

    // --- arithmetic ---

    [Theory]
    [InlineData(110, 100, 10.0)]   // higher
    [InlineData(90, 100, -10.0)]   // lower
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
    [InlineData("AMD EPYC 7763", "AMD EPYC 7763", true)]
    [InlineData("AMD EPYC 7763", "Intel Xeon Platinum 8370C", false)]
    public void SameCpu_compares_processor_name(string a, string b, bool expected)
        => Assert.Equal(expected, BenchCheck.SameCpu(new("Windows 10", a, "10"), new("Windows 10", b, "10")));

    [Theory]
    [InlineData(null, "AMD EPYC 7763")]
    [InlineData("", "AMD EPYC 7763")]
    [InlineData("AMD EPYC 7763", null)]
    public void SameCpu_never_matches_when_either_side_unrecorded(string? a, string? b)
        => Assert.False(BenchCheck.SameCpu(new("Windows 10", a, "10"), new("Windows 10", b, "10")));

    [Fact]
    public void SameCpu_placeholder_unknown_processor_never_matches_a_real_cpu()
        => Assert.False(BenchCheck.SameCpu(_unknownCpu, _epyc));

    // --- the gate: time axis, CPU-matched ---

    [Fact]
    public void Within_both_thresholds_passes() {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(_epyc, M(102)),                 // +2% vs rolling, +2% vs origin
            Loader(rolling: Side(_epyc, M(100)), origin: Side(_epyc, M(100))));

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(TimeClass.Ok, Assert.Single(report.Deltas).TimeClass);
    }

    [Fact]
    public void Acute_per_sprint_regression_fails() {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(_epyc, M(130)),                 // +30% vs rolling, low StdDev on both sides
            Loader(rolling: Side(_epyc, M(100)), origin: Side(_epyc, M(100))));

        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(TimeClass.PerSprintBreach, Assert.Single(report.Deltas).TimeClass);
    }

    [Fact]
    public void Slow_creep_trips_cumulative_even_when_per_sprint_is_in_tolerance() {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(_epyc, M(113)),                 // +2.7% vs rolling (110), +13% vs origin (100)
            Loader(rolling: Side(_epyc, M(110)), origin: Side(_epyc, M(100))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(TimeClass.CumulativeBreach, delta.TimeClass);
        Assert.True(delta.TimePerSprintPercent < 5.0);
        Assert.True(delta.TimeCumulativePercent > 12.0);
    }

    [Fact]
    public void Non_gating_category_regression_is_reported_not_failed() {
        var policy = new Policy(5.0, 12.0, 10.0, 85000, 3.0,
            [new PolicyCategory("compile", CompilePrefix, "compile.json", Gating: false)]);

        var report = BenchCheck.Evaluate(
            policy,
            fresh: Side(_epyc, M(200)),                 // +100%, but category is informational
            Loader(rolling: Side(_epyc, M(100)), origin: Side(_epyc, M(100))));

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(TimeClass.Informational, Assert.Single(report.Deltas).TimeClass);
    }

    [Fact]
    public void Missing_rolling_baseline_is_establishing_not_a_failure() {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(_epyc, M(100)),
            Loader(rolling: null, origin: null));

        Assert.Equal(Outcome.Pass, report.Outcome);
        var delta = Assert.Single(report.Deltas);
        Assert.Equal(TimeClass.NoBaseline, delta.TimeClass);
        Assert.Equal(AllocClass.NoBaseline, delta.AllocClass);
    }

    [Fact]
    public void New_benchmark_absent_from_baseline_is_informational() {
        var fresh = new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> {
            [Bench] = M(100),
            ["Grob.Benchmarks.Compile.CompileBenchmarks.Compile_BrandNew"] = M(999),
        });
        var rolling = Side(_epyc, M(100));

        var report = BenchCheck.Evaluate(PolicyWith(compileGating: true), fresh, Loader(rolling, rolling));

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Contains(report.Deltas, d => d.TimeClass == TimeClass.NewBenchmark && d.AllocClass == AllocClass.NewBenchmark);
    }

    [Fact]
    public void Missing_origin_skips_cumulative_axis_without_failing() {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(_epyc, M(103)),                 // +3% vs rolling, under the per-sprint gate
            Loader(rolling: Side(_epyc, M(100)), origin: null));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Null(delta.TimeCumulativePercent);
        Assert.Equal(TimeClass.Ok, delta.TimeClass);
    }

    // --- allocation axis (D-333, test rows 1-3) ---

    [Fact]
    public void Allocation_regression_trips() {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true, allocPercent: 10.0),
            fresh: Side(_epyc, M(100, bytes: 12000)),           // +20% vs 10000-byte baseline
            Loader(rolling: Side(_epyc, M(100, bytes: 10000)), origin: Side(_epyc, M(100, bytes: 10000))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(AllocClass.PerSprintBreach, delta.AllocClass);
    }

    [Fact]
    public void Allocation_within_threshold_passes() {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true, allocPercent: 10.0),
            fresh: Side(_epyc, M(100, bytes: 10500)),           // +5% vs 10000-byte baseline
            Loader(rolling: Side(_epyc, M(100, bytes: 10000)), origin: Side(_epyc, M(100, bytes: 10000))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(AllocClass.Ok, delta.AllocClass);
    }

    [Fact]
    public void Loh_tripwire_fires_even_on_informational_category() {
        var policy = new Policy(5.0, 12.0, 10.0, 85000, 3.0,
            [new PolicyCategory("vm", "Grob.Benchmarks.Vm", "vm.json", Gating: false)]);
        var name = "Grob.Benchmarks.Vm.VmBenchmarks.Run_ControlFlow";

        var report = BenchCheck.Evaluate(
            policy,
            fresh: new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 405418) }),
            _ => new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 405418) }));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(AllocClass.LohTripwireBreach, delta.AllocClass);
    }

    // --- time significance (D-333, test rows 4-5) ---

    [Fact]
    public void Time_delta_inside_noise_passes() {
        // The Sprint 6 Compile_TenPrints shape: ~8.7% delta, ~3.2% relative StdDev.
        // 3 * 3.2% ~= 9.6%, comfortably above 8.7%, so this must read Ok, not a breach.
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true, k: 3.0),
            fresh: Side(_epyc, M(6550, stdDev: 209.6)),          // 3.2% relative StdDev
            Loader(rolling: Side(_epyc, M(6025)), origin: Side(_epyc, M(6025))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(TimeClass.Ok, delta.TimeClass);
        Assert.True(delta.TimePerSprintPercent > 5.0);
    }

    [Fact]
    public void Time_delta_clearing_noise_trips() {
        // A genuine acute regression: +30% against a tight (~1%) relative StdDev
        // on both sides — well outside even a 3-sigma noise band.
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true, k: 3.0),
            fresh: Side(_epyc, M(130, stdDev: 1.3)),
            Loader(rolling: Side(_epyc, M(100, stdDev: 1.0)), origin: Side(_epyc, M(100, stdDev: 1.0))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(TimeClass.PerSprintBreach, delta.TimeClass);
    }

    // --- CPU-identity axis-split (D-333, test rows 6-8) ---

    [Fact]
    public void Cpu_match_time_gates_normally() {
        var passReport = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(_epyc, M(102)),
            Loader(rolling: Side(_epyc, M(100)), origin: Side(_epyc, M(100))));
        Assert.Equal(TimeClass.Ok, Assert.Single(passReport.Deltas).TimeClass);

        var breachReport = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(_epyc, M(130)),
            Loader(rolling: Side(_epyc, M(100)), origin: Side(_epyc, M(100))));
        Assert.Equal(TimeClass.PerSprintBreach, Assert.Single(breachReport.Deltas).TimeClass);
    }

    [Fact]
    public void Cpu_mismatch_time_informational_allocation_still_gates() {
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true, allocPercent: 10.0),
            fresh: Side(_xeon, M(140, bytes: 12000)),            // +40% time, +20% alloc
            Loader(rolling: Side(_epyc, M(100, bytes: 10000)), origin: Side(_epyc, M(100, bytes: 10000))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);           // allocation still fails the gate
        Assert.Equal(TimeClass.CpuMismatch, delta.TimeClass);       // time never breaches on CPU mismatch
        Assert.Equal(AllocClass.PerSprintBreach, delta.AllocClass);
    }

    [Fact]
    public void Proven_cross_cpu_fixture_matches_the_real_run() {
        // The real post-Interlude-1 verification run: EPYC-anchored compile baseline
        // (rolling and origin) vs an Intel Xeon fresh run. Allocation is byte-identical
        // (7864 B / 14480 B); time is +25-37% purely from the CPU swing.
        var name = "Grob.Benchmarks.Compile.CompileBenchmarks.Compile_TwoExpressions";
        var rolling = new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(2900.98, bytes: 7864) });
        var origin = new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(2900.98, bytes: 7864) });
        var fresh = new BaselineSide(_xeon, new Dictionary<string, BenchmarkMeasurement> { [name] = M(3623.26, bytes: 7864) });

        var report = BenchCheck.Evaluate(PolicyWith(compileGating: true), fresh, Loader(rolling, origin));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(TimeClass.CpuMismatch, delta.TimeClass);
        Assert.Equal(AllocClass.Ok, delta.AllocClass);
        Assert.Equal(0.0, delta.AllocPercent);
    }

    [Fact]
    public void Cumulative_axis_also_reads_cpu_mismatch_when_only_origin_cpu_is_unrecorded() {
        // The real compile.origin.json wrinkle: rolling's CPU matches fresh, but the
        // frozen origin predates CPU provenance ("Unknown processor"). The cumulative
        // axis must not silently compare across an unverifiable CPU gap.
        var report = BenchCheck.Evaluate(
            PolicyWith(compileGating: true),
            fresh: Side(_epyc, M(113)),                          // matches rolling's CPU
            Loader(rolling: Side(_epyc, M(110)), origin: Side(_unknownCpu, M(100))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(TimeClass.CpuMismatch, delta.TimeClass);
        Assert.NotNull(delta.TimeCumulativePercent);   // still computed and reported, just not gated
    }
}
