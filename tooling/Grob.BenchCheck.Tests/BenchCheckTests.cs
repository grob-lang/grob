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

    private static Policy PolicyWith(bool allocGating, double allocPercent = 10.0, double? allocationCeilingBytes = 85000, double k = 3.0) => new(
        PerSprintPercent: 5.0,
        CumulativePercent: 12.0,
        AllocPercent: allocPercent,
        TimeSignificanceK: k,
        Categories: [new PolicyCategory("compile", CompilePrefix, "compile.json", allocGating, allocationCeilingBytes)]);

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

    [Fact]
    public void SameCpu_placeholder_unknown_processor_never_matches_itself()
        => Assert.False(BenchCheck.SameCpu(_unknownCpu, _unknownCpu));

    // --- the gate: time never gates, on any category, by construction (D-395/D-396) ---

    [Fact]
    public void Within_both_thresholds_passes() {
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true),
            fresh: Side(_epyc, M(102)),                 // +2% vs rolling, +2% vs origin
            Loader(rolling: Side(_epyc, M(100)), origin: Side(_epyc, M(100))));

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(TimeClass.Informational, Assert.Single(report.Deltas).TimeClass);
    }

    [Fact]
    public void No_category_ever_gates_on_time_even_on_a_severe_regression() {
        // Mutation-verified: this must fail if ClassifyTime is reverted to consult
        // any gating flag, CPU match or significance threshold. A deliberately huge
        // per-sprint AND cumulative delta, with allocGating on and 0% allocation
        // delta, must still pass — time cannot gate, full stop (D-395 Q1).
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true),
            fresh: Side(_epyc, M(300, stdDev: 1.0)),    // +200% vs rolling and origin, tight StdDev
            Loader(rolling: Side(_epyc, M(100, stdDev: 1.0)), origin: Side(_epyc, M(100, stdDev: 1.0))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(TimeClass.Informational, delta.TimeClass);
        Assert.True(delta.TimePerSprintPercent > 100.0);
        Assert.True(delta.TimeCumulativePercent > 100.0);
    }

    [Fact]
    public void Run_30720384069_scenario_is_informational_not_a_breach() {
        // Regression test for the false positive D-395 diagnosed: compile category,
        // gating:true, +12.0% per-sprint time delta against ~2.02% within-run relative
        // StdDev (well past the old max(5.0%, 3*2.02%)=6.06% threshold) and a 0.0%
        // allocation delta. Must read Pass/Informational now, not PerSprintBreach.
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true, allocPercent: 10.0),
            fresh: Side(_epyc, M(5.554, stdDev: 0.1002, bytes: 9856)),   // +12.0% vs rolling (4.958), 0% alloc delta
            Loader(rolling: Side(_epyc, M(4.958, bytes: 9856)), origin: Side(_epyc, M(4.958, bytes: 9856))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(TimeClass.Informational, delta.TimeClass);
        Assert.Equal(AllocClass.Ok, delta.AllocClass);
        Assert.Equal(0.0, delta.AllocPercent);
    }

    [Fact]
    public void Allocation_percent_breach_on_compile_still_fails_the_gate() {
        // The converse the prompt requires: decoupling time from allocation-percent
        // must not silence allocation-percent too. Mutation-verified — fails if
        // AllocGating stops driving ClassifyAlloc.
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true, allocPercent: 10.0),
            fresh: Side(_epyc, M(100, bytes: 12000)),           // +20% alloc vs 10000-byte baseline, time flat
            Loader(rolling: Side(_epyc, M(100, bytes: 10000)), origin: Side(_epyc, M(100, bytes: 10000))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(TimeClass.Informational, delta.TimeClass);
        Assert.Equal(AllocClass.PerSprintBreach, delta.AllocClass);
    }

    [Fact]
    public void Non_gating_category_allocation_percent_is_reported_not_failed() {
        // allocGating:false is the only thing this flag still controls — time is
        // informational unconditionally regardless of it, so this test targets the
        // allocation-percent axis, the flag's sole remaining purpose.
        var policy = new Policy(5.0, 12.0, 10.0, 3.0,
            [new PolicyCategory("compile", CompilePrefix, "compile.json", AllocGating: false)]);

        var report = BenchCheck.Evaluate(
            policy,
            fresh: Side(_epyc, M(200, bytes: 20000)),   // +100% time and +100% alloc, but category is informational
            Loader(rolling: Side(_epyc, M(100, bytes: 10000)), origin: Side(_epyc, M(100, bytes: 10000))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(TimeClass.Informational, delta.TimeClass);
        Assert.Equal(AllocClass.Informational, delta.AllocClass);
    }

    [Fact]
    public void Missing_rolling_baseline_is_establishing_not_a_failure() {
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true),
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

        var report = BenchCheck.Evaluate(PolicyWith(allocGating: true), fresh, Loader(rolling, rolling));

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Contains(report.Deltas, d => d.TimeClass == TimeClass.NewBenchmark && d.AllocClass == AllocClass.NewBenchmark);
    }

    [Fact]
    public void New_benchmark_exceeding_allocation_ceiling_still_fails_the_gate() {
        // A fresh-only benchmark (absent from the rolling baseline) must not bypass
        // the absolute allocation ceiling — the contract is "any benchmark, gating or
        // not" (D-333). Otherwise a newly added, over-allocating benchmark would
        // silently pass and be frozen into the next baseline (the D-332 class of
        // defect).
        var fresh = new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> {
            [Bench] = M(100),
            ["Grob.Benchmarks.Compile.CompileBenchmarks.Compile_BrandNew"] = M(999, bytes: 90000),
        });
        var rolling = Side(_epyc, M(100));

        var report = BenchCheck.Evaluate(PolicyWith(allocGating: true, allocationCeilingBytes: 85000), fresh, Loader(rolling, rolling));

        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Contains(report.Deltas, d => d.FullName.Contains("BrandNew") && d.AllocClass == AllocClass.CeilingBreach);
    }

    [Fact]
    public void Missing_origin_skips_cumulative_axis_without_failing() {
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true),
            fresh: Side(_epyc, M(103)),                 // +3% vs rolling, time never gates regardless
            Loader(rolling: Side(_epyc, M(100)), origin: null));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Null(delta.TimeCumulativePercent);
        Assert.Equal(TimeClass.Informational, delta.TimeClass);
    }

    // --- allocation axis (D-333, test rows 1-3) ---

    [Fact]
    public void Allocation_regression_trips() {
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true, allocPercent: 10.0),
            fresh: Side(_epyc, M(100, bytes: 12000)),           // +20% vs 10000-byte baseline
            Loader(rolling: Side(_epyc, M(100, bytes: 10000)), origin: Side(_epyc, M(100, bytes: 10000))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(AllocClass.PerSprintBreach, delta.AllocClass);
    }

    [Fact]
    public void Allocation_within_threshold_passes() {
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true, allocPercent: 10.0),
            fresh: Side(_epyc, M(100, bytes: 10500)),           // +5% vs 10000-byte baseline
            Loader(rolling: Side(_epyc, M(100, bytes: 10000)), origin: Side(_epyc, M(100, bytes: 10000))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(AllocClass.Ok, delta.AllocClass);
    }

    [Fact]
    public void Ceiling_fires_even_on_informational_category() {
        var policy = new Policy(5.0, 12.0, 10.0, 3.0,
            [new PolicyCategory("vm", "Grob.Benchmarks.Vm", "vm.json", AllocGating: false, AllocationCeilingBytes: 85000)]);
        var name = "Grob.Benchmarks.Vm.VmBenchmarks.Run_ControlFlow";

        var report = BenchCheck.Evaluate(
            policy,
            fresh: new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 405418) }),
            _ => new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 405418) }));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(AllocClass.CeilingBreach, delta.AllocClass);
    }

    // --- allocation ceiling: per-category default and per-benchmark overrides (D-390/D-391 phase 3b) ---

    [Fact]
    public void Benchmark_specific_ceiling_overrides_category_default() {
        // vm's shape: a small scalar-fixture default (D-391) with a much larger
        // per-benchmark override for a for...in fixture that would otherwise trip
        // the small default outright.
        var category = new PolicyCategory("vm", "Grob.Benchmarks.Vm", "vm.json", AllocGating: false,
            AllocationCeilingBytes: 4700,
            BenchmarkAllocationCeilings: new Dictionary<string, double> {
                ["Grob.Benchmarks.Vm.VmBenchmarks.Run_ArrayForIn"] = 637900,
            });
        var policy = new Policy(5.0, 12.0, 10.0, 3.0, [category]);
        var name = "Grob.Benchmarks.Vm.VmBenchmarks.Run_ArrayForIn";

        var report = BenchCheck.Evaluate(
            policy,
            fresh: new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 531616) }),
            _ => new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 531616) }));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(AllocClass.Informational, delta.AllocClass); // vm is non-gating on allocation percent; the ceiling is the only axis that can fail it here, and it doesn't
    }

    [Fact]
    public void Benchmark_specific_ceiling_still_fires_when_inflated_value_exceeds_it() {
        // Proves the override is live in the *failing* direction, which needs the
        // override to be stricter than the category default — the reverse of vm's
        // real shape, deliberately, so that the override is the only thing that can
        // fail this row. The inflated value clears the generous category default and
        // breaches only the per-benchmark ceiling; delete BenchmarkAllocationCeilings
        // and this test goes green-to-red, which is what makes it load-bearing.
        // Benchmark_specific_ceiling_overrides_category_default covers the passing
        // direction on vm's real (default-stricter-than-override) shape.
        var category = new PolicyCategory("vm", "Grob.Benchmarks.Vm", "vm.json", AllocGating: false,
            AllocationCeilingBytes: 900_000,
            BenchmarkAllocationCeilings: new Dictionary<string, double> {
                ["Grob.Benchmarks.Vm.VmBenchmarks.Run_ArrayForIn"] = 637900,
            });
        var policy = new Policy(5.0, 12.0, 10.0, 3.0, [category]);
        var name = "Grob.Benchmarks.Vm.VmBenchmarks.Run_ArrayForIn";

        var report = BenchCheck.Evaluate(
            policy,
            fresh: new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 700000) }),
            _ => new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 531616) }));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(AllocClass.CeilingBreach, delta.AllocClass);
    }

    [Fact]
    public void Ceiling_breach_still_reports_the_rolling_allocation_percentage() {
        // The row that failed the gate is the one a reader most wants the rolling
        // delta for. Classification as a ceiling breach must not blank the Δ alloc
        // column when both sides reported bytes and the percentage is computable.
        var category = new PolicyCategory("vm", "Grob.Benchmarks.Vm", "vm.json", AllocGating: false,
            AllocationCeilingBytes: 637900);
        var policy = new Policy(5.0, 12.0, 10.0, 3.0, [category]);
        var name = "Grob.Benchmarks.Vm.VmBenchmarks.Run_ArrayForIn";

        var report = BenchCheck.Evaluate(
            policy,
            fresh: new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 700000) }),
            _ => new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 500000) }));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(AllocClass.CeilingBreach, delta.AllocClass);
        Assert.Equal(40.0, delta.AllocPercent!.Value, 3);
    }

    [Fact]
    public void Ceiling_breach_reports_no_percentage_when_the_rolling_side_has_no_bytes() {
        // The complement: with nothing to delta against, the breach still fires but
        // the percentage stays null rather than being invented.
        var category = new PolicyCategory("vm", "Grob.Benchmarks.Vm", "vm.json", AllocGating: false,
            AllocationCeilingBytes: 637900);
        var policy = new Policy(5.0, 12.0, 10.0, 3.0, [category]);
        var name = "Grob.Benchmarks.Vm.VmBenchmarks.Run_ArrayForIn";

        var report = BenchCheck.Evaluate(
            policy,
            fresh: new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 700000) }),
            _ => new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100) }));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(AllocClass.CeilingBreach, delta.AllocClass);
        Assert.Null(delta.AllocPercent);
    }

    [Fact]
    public void Category_default_ceiling_governs_a_benchmark_without_its_own_override() {
        var category = new PolicyCategory("vm", "Grob.Benchmarks.Vm", "vm.json", AllocGating: false,
            AllocationCeilingBytes: 4700,
            BenchmarkAllocationCeilings: new Dictionary<string, double> {
                ["Grob.Benchmarks.Vm.VmBenchmarks.Run_ArrayForIn"] = 637900,
            });
        var policy = new Policy(5.0, 12.0, 10.0, 3.0, [category]);
        var name = "Grob.Benchmarks.Vm.VmBenchmarks.Run_DeclAndArith"; // no per-benchmark override

        var report = BenchCheck.Evaluate(
            policy,
            fresh: new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 5000) }),
            _ => new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 2344) }));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);
        Assert.Equal(AllocClass.CeilingBreach, delta.AllocClass);
    }

    [Fact]
    public void Category_with_no_configured_ceiling_never_breaches() {
        // endToEnd's shape: declared, no ceiling set yet (F8 open, zero fresh
        // benchmarks today) — an unconfigured ceiling must not silently breach on
        // whatever allocation a benchmark happens to report.
        var category = new PolicyCategory("endToEnd", "Grob.Benchmarks.EndToEnd", "endToEnd.json", AllocGating: false);
        var policy = new Policy(5.0, 12.0, 10.0, 3.0, [category]);
        var name = "Grob.Benchmarks.EndToEnd.EndToEndBenchmarks.Run_Whatever";

        var report = BenchCheck.Evaluate(
            policy,
            fresh: new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 10_000_000) }),
            _ => new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 10_000_000) }));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(AllocClass.Informational, delta.AllocClass); // endToEnd is non-gating too; a real breach would still show as CeilingBreach if a ceiling existed
    }

    // --- attribution category (D-385/D-386 Q5', phase 3a) ---

    [Fact]
    public void Attribution_category_large_regression_is_informational() {
        // The attr-* fixtures are instruments, not guards (D-386 Q5') — proves a
        // deliberately large time+alloc delta in the "attribution" category never
        // fails the gate, the same way Non_gating_category_regression_is_reported_not_failed
        // proves it generically, but naming the real category this increment adds.
        var policy = new Policy(5.0, 12.0, 10.0, 3.0,
            [new PolicyCategory("attribution", "Grob.Benchmarks.Attribution", "attribution.json", AllocGating: false)]);
        var name = "Grob.Benchmarks.Attribution.AttributionBenchmarks.Run_AttrNative";

        var report = BenchCheck.Evaluate(
            policy,
            fresh: new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(200, bytes: 20000) }), // +100% time, +100% alloc
            _ => new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(100, bytes: 10000) }));

        Assert.Equal(Outcome.Pass, report.Outcome);
        var delta = Assert.Single(report.Deltas);
        Assert.Equal(TimeClass.Informational, delta.TimeClass);
        Assert.Equal(AllocClass.Informational, delta.AllocClass);
    }

    [Fact]
    public void Attribution_new_fixtures_report_as_new_benchmark() {
        // attr-map-build, the empty-body snapshot fixture and attr-array-dispatch (the
        // growth-free control added in PR #171 review) join four pre-existing attr-*
        // fixtures in this increment. Against a rolling baseline that only knows the
        // four, all three new ones must classify as NewBenchmark, not a regression —
        // the same path BenchCheck already handles correctly (phase 2's "Correctly
        // working, do not fix" note).
        var policy = new Policy(5.0, 12.0, 10.0, 3.0,
            [new PolicyCategory("attribution", "Grob.Benchmarks.Attribution", "attribution.json", AllocGating: false)]);
        const string prefix = "Grob.Benchmarks.Attribution.AttributionBenchmarks.";
        var existing = new[] { "Run_AttrEmpty", "Run_AttrRange", "Run_AttrNative", "Run_AttrBuild" };
        var rollingMeasurements = existing.ToDictionary(n => prefix + n, _ => M(100, bytes: 10000));
        var rolling = new BaselineSide(_epyc, rollingMeasurements);

        var freshMeasurements = new Dictionary<string, BenchmarkMeasurement>(rollingMeasurements) {
            [prefix + "Run_AttrMapBuild"] = M(150, bytes: 15000),
            [prefix + "Run_AttrSnapshotEmpty"] = M(120, bytes: 12000),
            [prefix + "Run_AttrArrayDispatch"] = M(130, bytes: 13000),
        };
        var fresh = new BaselineSide(_epyc, freshMeasurements);

        var report = BenchCheck.Evaluate(policy, fresh, _ => rolling);

        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Contains(report.Deltas, d => d.FullName == prefix + "Run_AttrMapBuild" && d.TimeClass == TimeClass.NewBenchmark && d.AllocClass == AllocClass.NewBenchmark);
        Assert.Contains(report.Deltas, d => d.FullName == prefix + "Run_AttrSnapshotEmpty" && d.TimeClass == TimeClass.NewBenchmark && d.AllocClass == AllocClass.NewBenchmark);
        Assert.Contains(report.Deltas, d => d.FullName == prefix + "Run_AttrArrayDispatch" && d.TimeClass == TimeClass.NewBenchmark && d.AllocClass == AllocClass.NewBenchmark);
        Assert.All(existing, n => Assert.Contains(report.Deltas, d => d.FullName == prefix + n && d.TimeClass == TimeClass.Informational));
    }

    // --- CPU identity: still an allocation-only concern now time never gates (D-333, D-395/D-396) ---

    [Fact]
    public void Cpu_mismatch_time_informational_allocation_still_gates() {
        // Allocation was never CPU-gated to begin with (D-333) — this proves that's
        // still true post-decoupling. Time is Informational for the CPU-matched and
        // CPU-mismatched case alike now, so this test's only remaining point is that
        // a CPU swing cannot suppress an allocation-percent breach.
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true, allocPercent: 10.0),
            fresh: Side(_xeon, M(140, bytes: 12000)),            // +40% time, +20% alloc
            Loader(rolling: Side(_epyc, M(100, bytes: 10000)), origin: Side(_epyc, M(100, bytes: 10000))));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Regression, report.Outcome);           // allocation still fails the gate
        Assert.Equal(TimeClass.Informational, delta.TimeClass);
        Assert.Equal(AllocClass.PerSprintBreach, delta.AllocClass);
    }

    [Fact]
    public void Proven_cross_cpu_fixture_matches_the_real_run() {
        // The real post-Interlude-1 verification run: EPYC-anchored compile baseline
        // (rolling and origin) vs an Intel Xeon fresh run. Allocation is byte-identical
        // (7864 B); time is +25-37% purely from the CPU swing, and now reads
        // Informational rather than the old CpuMismatch classification — still
        // computed and reported (Δ time is not blanked), just never a breach.
        var name = "Grob.Benchmarks.Compile.CompileBenchmarks.Compile_TwoExpressions";
        var rolling = new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(2900.98, bytes: 7864) });
        var origin = new BaselineSide(_epyc, new Dictionary<string, BenchmarkMeasurement> { [name] = M(2900.98, bytes: 7864) });
        var fresh = new BaselineSide(_xeon, new Dictionary<string, BenchmarkMeasurement> { [name] = M(3623.26, bytes: 7864) });

        var report = BenchCheck.Evaluate(PolicyWith(allocGating: true), fresh, Loader(rolling, origin));

        var delta = Assert.Single(report.Deltas);
        Assert.Equal(Outcome.Pass, report.Outcome);
        Assert.Equal(TimeClass.Informational, delta.TimeClass);
        Assert.NotNull(delta.TimePerSprintPercent);
        Assert.Equal(AllocClass.Ok, delta.AllocClass);
        Assert.Equal(0.0, delta.AllocPercent);
    }

    [Fact]
    public void Cpu_mismatch_still_produces_an_explanatory_note() {
        // The CPU-identity guard's remaining job: even though the classification is
        // always Informational now, the Notes list still explains *why* a reader
        // might see a large, non-gating Δ time (D-333's guard is reporting-only now,
        // not classification-only-when-gating).
        var report = BenchCheck.Evaluate(
            PolicyWith(allocGating: true),
            fresh: Side(_xeon, M(140)),
            Loader(rolling: Side(_epyc, M(100)), origin: Side(_epyc, M(100))));

        Assert.Contains(report.Notes, n => n.Contains("CPU mismatch", StringComparison.Ordinal));
    }
}
