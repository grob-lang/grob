using Grob.BenchCheck;
using Xunit;

namespace Grob.BenchCheck.Tests;

public sealed class CliRenderTests {
    private static readonly BdnHostEnvironmentInfo _host =
        new("Windows 10 (10.0.22621)", "Intel Xeon Platinum 8370C", "10.0.0");

    private static readonly Policy _policy = new(5.0, 12.0, []);
    private static readonly BaselineSide _fresh = new(_host, new Dictionary<string, double>());
    private static readonly BaselineSide _freshNoHost = new(null, new Dictionary<string, double>());

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

    [Fact]
    public void CannotCompare_outcome_renders_CANNOT_COMPARE_header() {
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.CannotCompare, [], []));
        Assert.Contains("CANNOT COMPARE", rendered);
    }

    // --- delta status labels ---

    [Theory]
    [InlineData(DeltaClass.Ok, "ok")]
    [InlineData(DeltaClass.Informational, "info")]
    [InlineData(DeltaClass.NewBenchmark, "new")]
    [InlineData(DeltaClass.NoBaseline, "establishing")]
    [InlineData(DeltaClass.PerSprintBreach, "**per-sprint breach**")]
    [InlineData(DeltaClass.CumulativeBreach, "**cumulative breach**")]
    [InlineData(DeltaClass.RunnerMismatch, "**runner mismatch**")]
    public void Delta_class_renders_correct_status_label(DeltaClass cls, string expectedLabel) {
        var delta = new BenchmarkDelta("cat", "A.B.MyBench", null, null, cls);
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [delta], []));
        Assert.Contains(expectedLabel, rendered);
    }

    // --- short name ---

    [Fact]
    public void Benchmark_full_name_is_shortened_to_last_segment() {
        var delta = new BenchmarkDelta("cat", "Grob.Benchmarks.Compile.CompileBenchmarks.Compile_Foo", 1.0, null, DeltaClass.Ok);
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [delta], []));
        Assert.Contains("Compile_Foo", rendered);
        Assert.DoesNotContain("Grob.Benchmarks.Compile", rendered);
    }

    [Fact]
    public void Benchmark_name_without_dot_renders_as_is() {
        var delta = new BenchmarkDelta("cat", "NoDotName", 0.0, null, DeltaClass.Ok);
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [delta], []));
        Assert.Contains("NoDotName", rendered);
    }

    // --- percentage formatting ---

    [Fact]
    public void Null_percent_renders_em_dash_placeholder() {
        var delta = new BenchmarkDelta("cat", "A.B.Bench", null, null, DeltaClass.NoBaseline);
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [delta], []));
        Assert.Contains("—", rendered);
    }

    [Fact]
    public void Positive_percent_renders_with_plus_sign() {
        var delta = new BenchmarkDelta("cat", "A.B.Bench", 5.5, null, DeltaClass.PerSprintBreach);
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Regression, [delta], []));
        Assert.Contains("+5.5%", rendered);
    }

    [Fact]
    public void Negative_percent_renders_with_minus_sign() {
        var delta = new BenchmarkDelta("cat", "A.B.Bench", -3.0, null, DeltaClass.Ok);
        var rendered = Cli.Render(_policy, _fresh, new EvaluationReport(Outcome.Pass, [delta], []));
        Assert.Contains("-3.0%", rendered);
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
    }
}
