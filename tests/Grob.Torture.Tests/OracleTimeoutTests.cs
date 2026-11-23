using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// A phase the VM's token does not reach is bounded by the watchdog, and running past it is a
/// timeout finding named by the phase it stalled in (gate report §4.4, ruling Q1).
/// </summary>
public sealed class OracleTimeoutTests {
    [Fact]
    public void Run_CheckPhaseThatNeverEnds_IsTimeoutFindingForCheck() {
        // Warm the pipeline first: a cold catalogue build and JIT can outlast a short watchdog.
        new Oracle(OracleBudget.Ci).Run("print(1)\n");
        using var release = new ManualResetEventSlim();
        var budget = new OracleBudget(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500));
        var oracle = new Oracle(budget, phaseHook: p => {
            if (p == PipelinePhase.Check) release.Wait();
        });

        try {
            OracleResult result = oracle.Run("print(1)\n");

            Assert.Equal(OutcomeKind.Finding, result.Outcome);
            Finding finding = Assert.Single(result.Findings);
            Assert.Equal(new Signature(PipelinePhase.Check, FindingClass.Timeout, "watchdog"), finding.Signature);
        } finally {
            release.Set();
        }
    }
}
