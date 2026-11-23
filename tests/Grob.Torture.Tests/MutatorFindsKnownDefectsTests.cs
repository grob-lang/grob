using Grob.Torture.Mutation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// Never trust a green campaign (prompt §3): a campaign that finds nothing proves only that its
/// operators found nothing. Before the first campaign the oracle must find R-58 and R-60 from a
/// seed through the mutator, not only from a hand-written reproducer. Each test pins one program
/// by its id, from a seed of its own so that a change to a corpus file cannot move it.
/// </summary>
/// <remarks>
/// These tests depend on open defects, as no other guard does: each expects the finding its row
/// describes. When R-58 or R-60 closes, the mutant stops failing, and the test is updated to the
/// next open defect the same operator reaches or removed, with the row's known entries.
/// </remarks>
public sealed class MutatorFindsKnownDefectsTests {
    private static OracleResult Run(Mutant mutant) => new Oracle(OracleBudget.Ci).Run(mutant.Source, "pin.grob");

    [Fact]
    public void ReplaceValue_ExpressionInitialiserOfAFloatBinding_ReachesR58() {
        // R-58: an int where a float is expected is accepted and not widened, so `x / 2` stops.
        var seed = new Seed("pin-r58@1", SeedKind.Fixture, "pin", "f := -2.5\nx: float := f.abs()\nprint(x / 2)\n");

        Mutant mutant = new Mutator(CorpusSlice.CampaignSeed).Mutate(seed, Operator("replace-value"), 3)!;

        Assert.Equal("pin-r58@1/replace-value/3", mutant.ProgramId);
        Assert.Equal("f := -2.5\nx: float := 1\nprint(x / 2)\n", mutant.Source);
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
