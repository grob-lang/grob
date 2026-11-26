using Grob.Torture.Mutation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// Never trust a green campaign (prompt §3): a campaign that finds nothing proves only that its
/// operators found nothing. Before the first campaign the oracle must find R-63 (R-58 until D-460
/// closed it) and R-60 from a seed through the mutator, not only from a hand-written reproducer.
/// Each test pins one program by its id, from a seed of its own so that a change to a corpus file
/// cannot move it.
/// </summary>
/// <remarks>
/// These tests depend on open defects, as no other guard does: each expects the finding its row
/// describes. When R-63 or R-60 closes, the mutant stops failing, and the test is updated to the
/// next open defect the same operator reaches or removed, with the row's known entries.
/// </remarks>
public sealed class MutatorFindsKnownDefectsTests {
    private static OracleResult Run(Mutant mutant) => new Oracle(OracleBudget.Ci).Run(mutant.Source, "pin.grob");

    [Fact]
    public void ReplaceValue_ArgumentOfALambdaWithAnUnknownOperand_ReachesR63() {
        // R-63: a lambda with no type context compiles `n + 2.5` with an assumed operand type, so
        // a call given an int stops. This pin took over from R-58 when D-460 closed it: the same
        // operator reached R-58 through `x: float := f.abs()` becoming `x: float := 1`.
        var seed = new Seed("pin-r63@1", SeedKind.Fixture, "pin", "f := (n) => n + 2.5\nprint(f(2.5))\n");

        Mutant mutant = new Mutator(CorpusSlice.CampaignSeed).Mutate(seed, Operator("replace-value"), 10)!;

        Assert.Equal("pin-r63@1/replace-value/10", mutant.ProgramId);
        Assert.Equal("f := (n) => n + 2.5\nprint(f(1))\n", mutant.Source);
        Finding finding = Assert.Single(Run(mutant).Findings);
        Assert.Equal("Run|Exception|GrobInternalException: GrobValue kind mismatch: expected Float, actual Int.", finding.Signature.Key);
    }

    [Fact]
    public void ReplaceValue_ArgumentOfAnArrayMethodThatTakesAFunction_ReachesR60() {
        // R-60: `filter` does not check that its argument is a function, so the VM calls a float.
        var seed = new Seed("pin-r60@1", SeedKind.Fixture, "pin", "xs := [1, 2, 3]\nys := xs.filter(x => x > 1)\nprint(ys)\n");

        Mutant mutant = new Mutator(CorpusSlice.CampaignSeed).Mutate(seed, Operator("replace-value"), 3)!;

        Assert.Equal("pin-r60@1/replace-value/3", mutant.ProgramId);
        Assert.Equal("xs := [1, 2, 3]\nys := xs.filter(1.5)\nprint(ys)\n", mutant.Source);
        Finding finding = Assert.Single(Run(mutant).Findings);
        Assert.StartsWith("Run|Exception|GrobInternalException: InvokeCallable: value of kind Float is not callable.", finding.Signature.Key, StringComparison.Ordinal);
    }

    private static IMutationOperator Operator(string name) => Mutator.Operators.Single(o => o.Name == name);
}
