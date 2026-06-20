using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// End-to-end positive proof: running every check against the live, A1-reconciled
/// corpus passes. This is the single entry the console wrapper (Grob.DriftCheck)
/// also drives, so a green run here is a green local `dotnet run` too.
/// </summary>
public sealed class DriftGateTests {
    [Fact]
    public void RunAll_PassesAgainstTheReconciledCorpus() {
        var results = ConsistencyChecks.RunAll();

        var failures = results.Where(r => !r.Ok)
            .Select(r => r.Summarise())
            .ToList();

        Assert.True(failures.Count == 0,
            "Consistency drift gate found drift:" + Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void RunAll_CoversEveryRequiredCheck() {
        var names = ConsistencyChecks.RunAll().Select(r => r.Name).ToList();

        Assert.Contains("error-code count agreement", names);
        Assert.Contains("decisions-log lockstep", names);
        Assert.Contains("ADR reference integrity", names);
        Assert.Contains("OpCode completeness", names);
        Assert.Contains("TokenKind completeness", names);
        Assert.Contains("ErrorCatalog agreement guard (D-308)", names);
    }
}
