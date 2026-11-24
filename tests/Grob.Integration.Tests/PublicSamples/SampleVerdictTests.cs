using Xunit;

namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// <see cref="SampleVerdict"/> over real runs through <see cref="SampleRunner"/> (D-430 Decision 2
/// to Decision 4), one test per rule, so a verdict is never judged against a run the CLI would not
/// produce.
/// </summary>
public sealed class SampleVerdictTests {
    private static readonly SampleMarkers Bare = new(false, [], []);
    private static readonly SampleMarkers Fragment = new(true, [], []);
    private static readonly SampleMarkers Blocked = new(false, ["R-42"], []);

    // R-42: a two-parameter sort key selector passes the checker and crashes the VM. It
    // stands in for R-30, the regex-literal crash D-440 closed.
    private const string InternalErrorSource = "xs := [\"a\", \"bb\"]\nys := xs.sort((a, b) => a.length > b.length)\nprint(ys)\n";

    private static string? Check(SampleMarkers markers, string source) =>
        SampleVerdict.Check(markers, SampleRunner.Run(source, PublicSampleCorpus.FixtureEnvironment));

    [Fact]
    public void Bare_RunsClean_Passes() {
        Assert.Null(Check(Bare, "print(\"hello\")\n"));
    }

    [Fact]
    public void Bare_WritesADiagnostic_Fails() {
        Assert.StartsWith("writes E", Check(Bare, "n: int := \"text\"\n"));
    }

    [Fact]
    public void Bare_NonNamespaceE1001_Fails() {
        Assert.Equal("writes E1001 ('total')", Check(Bare, "print(total)\n"));
    }

    [Fact]
    public void Bare_ExitsNonZero_Fails() {
        Assert.Equal("exits 3 with no diagnostic", Check(Bare, "exit(3)\n"));
    }

    [Fact]
    public void Bare_ReportsAnInternalError_Fails() {
        string? failure = Check(Bare, InternalErrorSource);

        // The CLI reports it as E9001 (D-451 Decision 2), which the verdict reads as a diagnostic.
        Assert.Equal("writes E9001", failure);
    }

    [Fact]
    public void Bare_OnlyAnExcusedE1001_PassesWithoutExecuting() {
        // exit(4) would fail a block that ran; the excused E1001 stops the pipeline first.
        Assert.Null(Check(Bare, "files := fs.list(\".\")\nexit(4)\n"));
    }

    [Fact]
    public void Bare_ExcusedE1001AndAnotherDiagnostic_Fails() {
        Assert.Equal("writes E1001 ('total')", Check(Bare, "files := fs.list(\".\")\nprint(total)\n"));
    }

    [Fact]
    public void Fragment_OnlyE1001s_Passes() {
        Assert.Null(Check(Fragment, "print(total)\nprint(count)\n"));
    }

    [Fact]
    public void Fragment_NoDiagnostic_Fails() {
        Assert.StartsWith("is marked fragment but writes no diagnostic", Check(Fragment, "print(\"complete\")\n"));
    }

    [Fact]
    public void Fragment_OnlyExcusedE1001s_FailsAsACompleteProgram() {
        Assert.StartsWith("is marked fragment but writes no diagnostic", Check(Fragment, "files := fs.list(\".\")\n"));
    }

    [Fact]
    public void Fragment_WritesE1002_Fails() {
        Assert.StartsWith("is a fragment, which may write only E1001, but writes E1002", Check(Fragment, "x := 5\nprint(x.nope)\n"));
    }

    [Fact]
    public void Blocked_FailsItsClass_Passes() {
        Assert.Null(Check(Blocked, InternalErrorSource));
    }

    [Fact]
    public void Blocked_PassesItsClass_FailsWithRemoveTheToken() {
        Assert.Equal("passes its class but carries blocked-by=R-42; remove the token", Check(Blocked, "print(1)\n"));
    }
}
