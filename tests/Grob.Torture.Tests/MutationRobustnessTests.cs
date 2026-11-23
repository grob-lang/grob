using Grob.Torture.Mutation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// An operator never throws and never makes an edit that cannot be applied, whatever the program:
/// the oracle checks invalid programs as much as valid ones, so the mutator meets malformed seeds
/// and must read them as well as a well-formed one.
/// </summary>
public sealed class MutationRobustnessTests {
    public static TheoryData<string> Malformed => new() {
        string.Empty,
        "x :=\n",
        "x: := 1\n",
        "f(\n",
        "f(1,\n",
        "f(1, 2\n",
        "if (x != nil\n",
        "if (nil == x) {\n",
        "if (nil != x) {\n    print(x)\n}\n",
        "if (x == nil) {\n    print(1)\n}\n",
        "fn f(\n",
        "fn f(a: int, b): {\n}\n",
        "const c :=\n",
        "const c := 1 +\n",
        "type T {\n",
        "a + \n",
        "x := [1,\n",
        "s := \"unterminated\n",
        "x := #{ a: }\n",
        "s?.\n",
        "return\n",
        "x := ((((1))))\n",
    };

    [Theory]
    [MemberData(nameof(Malformed))]
    public void Operators_MalformedProgram_NeverThrowAndEveryEditApplies(string source) {
        MutationContext? context = MutationContext.TryCreate(source);
        Assert.NotNull(context);

        foreach (IMutationOperator mutationOperator in Mutator.Operators) {
            foreach (IReadOnlyList<SourceEdit> edits in mutationOperator.Candidates(context!)) {
                Assert.NotNull(SourceEdit.Apply(source, edits));
            }
        }
    }

    [Fact]
    public void Operators_EveryCorpusSeed_EveryCandidateAppliesCleanly() {
        string root = SeedCorpus.LocateRepositoryRoot(AppContext.BaseDirectory);
        var failures = new List<string>();
        foreach (Seed seed in SeedCorpus.Load(root)) {
            MutationContext? context = MutationContext.TryCreate(seed.Source);
            if (context is null) continue;

            foreach (IMutationOperator mutationOperator in Mutator.Operators) {
                IReadOnlyList<IReadOnlyList<SourceEdit>> candidates = mutationOperator.Candidates(context);
                // Every candidate of a small seed, and an even spread of the rest: the property is
                // about shapes, and a seed with thousands of candidates repeats them.
                int step = Math.Max(1, candidates.Count / 25);
                for (int i = 0; i < candidates.Count; i += step) {
                    try {
                        SourceEdit.Apply(seed.Source, candidates[i]);
                    } catch (ArgumentException ex) {
                        failures.Add($"{mutationOperator.Name} on {seed.Id}: {ex.Message}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, "edits that cannot be applied:\n  " + string.Join("\n  ", failures.Take(20)));
    }
}
