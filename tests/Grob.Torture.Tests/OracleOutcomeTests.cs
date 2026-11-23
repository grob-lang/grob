using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The allowed outcomes pass and the out-of-range <c>exit(n)</c> is a finding (prompt §1.1 and
/// §4; gate report §1.1).
/// </summary>
public sealed class OracleOutcomeTests {
    private static readonly OracleBudget Short = new(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10));

    private static OracleResult Run(string source) => new Oracle(Short).Run(source);

    [Fact]
    public void Run_CleanProgram_IsClean() {
        OracleResult result = Run("x := 1 + 2\nprint(x)\n");

        Assert.Equal(OutcomeKind.Clean, result.Outcome);
        Assert.Empty(result.Findings);
        Assert.Null(result.ExitCode);
    }

    [Fact]
    public void Run_TypeError_IsDiagnostics() {
        OracleResult result = Run("x: int := \"a\"\n");

        Assert.Equal(OutcomeKind.Diagnostics, result.Outcome);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Run_SyntaxError_IsDiagnostics() {
        OracleResult result = Run("x := (1 +\n");

        Assert.Equal(OutcomeKind.Diagnostics, result.Outcome);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Run_RuntimeError_IsRuntimeError() {
        OracleResult result = Run("xs := [1]\nprint(xs[5])\n");

        Assert.Equal(OutcomeKind.RuntimeError, result.Outcome);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Run_ExitInRange_IsExitWithItsCode() {
        OracleResult result = Run("exit(42)\n");

        Assert.Equal(OutcomeKind.Exit, result.Outcome);
        Assert.Equal(42, result.ExitCode);
        Assert.Empty(result.Findings);
    }

    [Theory]
    [InlineData("exit(300)\n", 300, "above-255")]
    [InlineData("exit(-1)\n", -1, "negative")]
    public void Run_ExitOutOfRange_IsExitRangeFindingByBucket(string source, int code, string bucket) {
        OracleResult result = Run(source);

        Assert.Equal(OutcomeKind.Finding, result.Outcome);
        Assert.Equal(code, result.ExitCode);
        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(new Signature(PipelinePhase.Run, FindingClass.ExitRange, bucket), finding.Signature);
    }

    [Fact]
    public void Run_EndlessLoop_IsBoundedNotAFinding() {
        OracleResult result = Run("while (true) {\n}\n");

        Assert.Equal(OutcomeKind.Bounded, result.Outcome);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Run_SameProgramTwice_GivesEqualResults() {
        const string source = "xs := [3, 1, 2]\nprint(xs)\nprint(math.random())\n";

        OracleResult first = Run(source);
        OracleResult second = Run(source);

        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.Findings, second.Findings);
    }
}
