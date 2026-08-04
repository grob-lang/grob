using System.Globalization;
using Grob.BenchCheck;
using Xunit;

namespace Grob.BenchCheck.Tests;

public sealed class CliRenderTests {
    private static readonly BdnHostEnvironmentInfo _host =
        new("Windows 10 (10.0.22621)", "Intel Xeon Platinum 8370C", "10.0.0");

    private static readonly Policy _policy = new(5.0, 12.0, 10.0, 3.0, []);
    private static readonly BaselineSide _fresh = new(_host, new Dictionary<string, BenchmarkMeasurement>());
    private static readonly BaselineSide _freshNoHost = new(null, new Dictionary<string, BenchmarkMeasurement>());

    private static BenchmarkDelta TimeDelta(TimeClass cls, double? perSprint = null, double? cumulative = null)
        => new("cat", "A.B.MyBench", perSprint, cumulative, cls, null, null, AllocClass.Ok);

    private static BenchmarkDelta AllocDelta(AllocClass cls, double? percent = null, double? bytes = null)
        => new("cat", "A.B.MyBench", 0.0, null, TimeClass.Informational, percent, bytes, cls);

    // --- verdict header ---

    [Fact]
    public void Pass_outcome_renders_PASS_header() {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [], []));
        Assert.Contains("PASS", rendered);
    }

    [Fact]
    public void Regression_outcome_renders_REGRESSION_header() {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Regression, [], []));
        Assert.Contains("REGRESSION", rendered);
    }

    // --- time-axis status labels ---

    [Theory]
    [InlineData(TimeClass.Informational, "info")]
    [InlineData(TimeClass.NewBenchmark, "new")]
    [InlineData(TimeClass.NoBaseline, "establishing")]
    public void Time_class_renders_correct_status_label(TimeClass cls, string expectedLabel) {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [TimeDelta(cls)], []));
        Assert.Contains(expectedLabel, rendered);
    }

    // --- allocation-axis status labels ---

    [Theory]
    [InlineData(AllocClass.Ok, "ok")]
    [InlineData(AllocClass.Informational, "info")]
    [InlineData(AllocClass.NewBenchmark, "new")]
    [InlineData(AllocClass.NoBaseline, "establishing")]
    [InlineData(AllocClass.PerSprintBreach, "**per-sprint breach**")]
    [InlineData(AllocClass.CeilingBreach, "**allocation ceiling**")]
    public void Alloc_class_renders_correct_status_label(AllocClass cls, string expectedLabel) {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [AllocDelta(cls)], []));
        Assert.Contains(expectedLabel, rendered);
    }

    // --- short name ---

    [Fact]
    public void Benchmark_full_name_is_shortened_to_last_segment() {
        var delta = new BenchmarkDelta("cat", "Grob.Benchmarks.Compile.CompileBenchmarks.Compile_Foo", 1.0, null, TimeClass.Informational, null, null, AllocClass.Ok);
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [delta], []));
        Assert.Contains("Compile_Foo", rendered);
        Assert.DoesNotContain("Grob.Benchmarks.Compile", rendered);
    }

    [Fact]
    public void Benchmark_name_without_dot_renders_as_is() {
        var delta = new BenchmarkDelta("cat", "NoDotName", 0.0, null, TimeClass.Informational, null, null, AllocClass.Ok);
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [delta], []));
        Assert.Contains("NoDotName", rendered);
    }

    // --- percentage / byte formatting ---

    [Fact]
    public void Null_percent_renders_em_dash_placeholder() {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [TimeDelta(TimeClass.NoBaseline)], []));
        Assert.Contains("—", rendered);
    }

    [Fact]
    public void Positive_percent_renders_with_plus_sign() {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Regression, [TimeDelta(TimeClass.Informational, perSprint: 5.5)], []));
        Assert.Contains("+5.5%", rendered);
    }

    [Fact]
    public void Negative_percent_renders_with_minus_sign() {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [TimeDelta(TimeClass.Informational, perSprint: -3.0)], []));
        Assert.Contains("-3.0%", rendered);
    }

    [Fact]
    public void Alloc_bytes_render_with_thousands_separator() {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Regression, [AllocDelta(AllocClass.CeilingBreach, bytes: 50265)], []));
        Assert.Contains("50,265 B", rendered);
    }

    [Fact]
    public void Alloc_bytes_use_invariant_culture_regardless_of_locale() {
        // The table body must format numbers with InvariantCulture, so separators
        // stay consistent regardless of the runner locale. de-DE swaps '.' and ',' —
        // a locale-sensitive format would render "50.265 B" here. (Per-category/
        // per-benchmark ceilings, D-391, mean there is no longer a single global
        // threshold figure in the header to anchor this on — the per-row Alloc
        // column is where the same formatting risk now lives.)
        var original = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Regression, [AllocDelta(AllocClass.CeilingBreach, bytes: 50265)], []));
            Assert.Contains("50,265 B", rendered);
        } finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    // --- notes ---

    [Fact]
    public void Notes_section_is_rendered_when_present() {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [], ["Something to note."]));
        Assert.Contains("> Something to note.", rendered);
    }

    [Fact]
    public void No_notes_blockquote_when_notes_empty() {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [], []));
        Assert.DoesNotContain("> ", rendered);
    }

    // --- host fallbacks ---

    [Fact]
    public void Null_host_renders_unknown_cpu_and_runtime_fallbacks() {
        var rendered = Cli.Render(_policy, _freshNoHost, new EvaluationReport(Outcome.Pass, [], []));
        Assert.Contains("unknown CPU", rendered);
        Assert.Contains("unknown runtime", rendered);
        Assert.Contains("unknown OS", rendered);
    }
}
