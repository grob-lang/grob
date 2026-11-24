using Xunit;

using Grob.Core;
using Grob.Runtime;
using Grob.Torture.Fakes;

namespace Grob.Torture.Tests;

/// <summary>
/// An exception from any phase is a finding named by its phase, and a Grob runtime error or
/// <c>exit(n)</c> is not (prompt §1.1 and §4). The phase hook makes a phase throw on demand, so
/// these tests do not depend on an open defect; R-58 and R-60 are pinned from their own
/// reproducers as well.
/// </summary>
public sealed class OracleExceptionTests {
    private static readonly OracleBudget Budget = new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10));

    public static TheoryData<string> Phases => [.. Enum.GetNames<PipelinePhase>()];

    [Theory]
    [MemberData(nameof(Phases))]
    public void Run_InternalErrorInPhase_IsExceptionFindingForThatPhase(string phaseName) {
        PipelinePhase phase = Enum.Parse<PipelinePhase>(phaseName);
        var oracle = new Oracle(Budget, phaseHook: p => {
            if (p == phase) throw new GrobInternalException("slot 12 holds 'abc'");
        });

        OracleResult result = oracle.Run("print(1)\n");

        Assert.Equal(OutcomeKind.Finding, result.Outcome);
        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(new Signature(phase, FindingClass.Exception, "GrobInternalException: slot # holds '…'"), finding.Signature);
        Assert.Equal("slot 12 holds 'abc'", finding.Message);
    }

    [Fact]
    public void Run_HostExceptionInCompilePhase_IsExceptionFinding() {
        var oracle = new Oracle(Budget, phaseHook: p => {
            if (p == PipelinePhase.Compile) throw new InvalidOperationException("no");
        });

        OracleResult result = oracle.Run("print(1)\n");

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(new Signature(PipelinePhase.Compile, FindingClass.Exception, "InvalidOperationException: no"), finding.Signature);
    }

    [Fact]
    public void Run_HostExceptionFromANative_IsRunPhaseFindingWithItsFrame() {
        var oracle = new Oracle(Budget, fakes: () => HostFakes.Create() with { Clock = new ThrowingClock() });

        OracleResult result = oracle.Run("print(date.now())\n");

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(new Signature(PipelinePhase.Run, FindingClass.Exception, "InvalidOperationException: clock unavailable"),
            finding.Signature);
        Assert.NotNull(finding.FirstFrame);
        Assert.StartsWith("Grob.Stdlib.", finding.FirstFrame, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_RuntimeErrorWithRegisteredCode_IsRuntimeError() {
        var oracle = new Oracle(Budget, phaseHook: p => {
            if (p == PipelinePhase.Run) throw new GrobRuntimeException(ErrorCatalog.E5101.Code, 1, "out of range");
        });

        OracleResult result = oracle.Run("print(1)\n");

        Assert.Equal(OutcomeKind.RuntimeError, result.Outcome);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Run_RuntimeErrorWithUnregisteredCode_IsUnregisteredFinding() {
        var oracle = new Oracle(Budget, phaseHook: p => {
            if (p == PipelinePhase.Run) throw new GrobRuntimeException("X0001", 1, "made up");
        });

        OracleResult result = oracle.Run("print(1)\n");

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(new Signature(PipelinePhase.Run, FindingClass.Unregistered, "X0001"), finding.Signature);
    }

    [Fact]
    public void Run_R58FloatBindingGivenInt_IsKindMismatchFinding() {
        OracleResult result = new Oracle(Budget).Run("x: float := 1\nprint(x / 2)\n");

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(R58Signature, finding.Signature);
    }

    [Fact]
    public void Run_R58FloatReturnGivenInt_SharesTheBindingsSignature() {
        OracleResult result = new Oracle(Budget).Run("fn f(): float {\n    return 1\n}\nprint(f() / 2)\n");

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(R58Signature, finding.Signature);
    }

    [Fact]
    public void Run_R60FilterGivenInt_IsNotCallableFinding() {
        OracleResult result = new Oracle(Budget).Run("xs := [1, 2]\nprint(xs.filter(5))\n");

        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(new Signature(PipelinePhase.Run, FindingClass.Exception,
            "GrobInternalException: InvokeCallable: value of kind Int is not callable. The type checker should have rejected the call site."),
            finding.Signature);
    }

    private static Signature R58Signature { get; } = new(PipelinePhase.Run, FindingClass.Exception,
        "GrobInternalException: GrobValue kind mismatch: expected Float, actual Int.");

    private sealed class ThrowingClock : IClock {
        public DateTime UtcNow => throw new InvalidOperationException("clock unavailable");

        public TimeZoneInfo LocalZone => TimeZoneInfo.Utc;
    }
}
