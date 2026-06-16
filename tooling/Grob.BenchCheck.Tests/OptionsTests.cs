using Grob.BenchCheck;
using Xunit;

namespace Grob.BenchCheck.Tests;

public sealed class OptionsTests {
    [Fact]
    public void Default_values_when_no_args() {
        var opts = Options.Parse([]);
        Assert.Equal("BenchmarkDotNet.Artifacts/results", opts.ResultsDir);
        Assert.Equal("bench/Grob.Benchmarks/baseline", opts.BaselineDir);
        Assert.Equal(Path.Join("bench/Grob.Benchmarks/baseline", "policy.json"), opts.PolicyPath);
    }

    [Fact]
    public void Results_flag_overrides_default() {
        var opts = Options.Parse(["--results", "my/results"]);
        Assert.Equal("my/results", opts.ResultsDir);
    }

    [Fact]
    public void Baseline_flag_overrides_default() {
        var opts = Options.Parse(["--baseline", "my/baseline"]);
        Assert.Equal("my/baseline", opts.BaselineDir);
    }

    [Fact]
    public void Policy_flag_overrides_default() {
        var opts = Options.Parse(["--policy", "my/policy.json"]);
        Assert.Equal("my/policy.json", opts.PolicyPath);
    }

    [Fact]
    public void Summary_flag_sets_summary_path() {
        var opts = Options.Parse(["--summary", "summary.md"]);
        Assert.Equal("summary.md", opts.SummaryPath);
    }

    [Fact]
    public void Policy_defaults_to_baseline_subpath_when_baseline_overridden() {
        var opts = Options.Parse(["--baseline", "custom/base"]);
        Assert.Equal(Path.Join("custom/base", "policy.json"), opts.PolicyPath);
    }

    [Fact]
    public void Explicit_policy_is_not_overridden_by_baseline() {
        var opts = Options.Parse(["--baseline", "custom/base", "--policy", "explicit/policy.json"]);
        Assert.Equal("explicit/policy.json", opts.PolicyPath);
    }

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    public void Help_flag_throws_InvalidDataException(string flag) {
        var ex = Assert.Throws<InvalidDataException>(() => Options.Parse([flag]));
        Assert.Contains("Usage:", ex.Message);
    }

    [Fact]
    public void Unknown_argument_throws_InvalidDataException() {
        var ex = Assert.Throws<InvalidDataException>(() => Options.Parse(["--bogus"]));
        Assert.Contains("Unknown argument", ex.Message);
    }

    [Fact]
    public void Missing_value_after_flag_throws_InvalidDataException() {
        var ex = Assert.Throws<InvalidDataException>(() => Options.Parse(["--results"]));
        Assert.Contains("Missing value", ex.Message);
    }

    [Fact]
    public void All_flags_together_produce_correct_options() {
        var opts = Options.Parse([
            "--results", "r",
            "--baseline", "b",
            "--policy", "p.json",
            "--summary", "s.md"]);
        Assert.Equal("r", opts.ResultsDir);
        Assert.Equal("b", opts.BaselineDir);
        Assert.Equal("p.json", opts.PolicyPath);
        Assert.Equal("s.md", opts.SummaryPath);
    }
}
