using Grob.Torture.Campaign;
using Grob.Torture.Generation;
using Grob.Torture.Mutation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// A campaign's plan (prompt §2 group 6): half generated programs and half mutants, fixed by the
/// campaign seed, with no source run twice, split across workers by program id.
/// </summary>
public sealed class CampaignPlanTests {
    private const ulong CampaignSeed = 7;

    private static Seed MakeSeed(int number) => new($"seed-{number}@{number:x16}", SeedKind.Fixture, $"seed-{number}.grob",
        $"fn f{number}(a: int, b: int): int {{\n    return a + b\n}}\ns := \"ab\"\nn: string? := nil\nx := f{number}(1, 2)\nprint(s.upper())\nconst k := 10 / 2\n");

    private static List<Seed> Corpus(int count) => [.. Enumerable.Range(0, count).Select(MakeSeed)];

    private static Dictionary<string, Seed> ById(IEnumerable<Seed> seeds) => seeds.ToDictionary(s => s.Id, StringComparer.Ordinal);

    [Fact]
    public void Build_SameArguments_GiveTheSamePlanTwice() {
        List<Seed> corpus = Corpus(10);

        Assert.Equal(CampaignPlan.Build(CampaignSeed, 60, corpus), CampaignPlan.Build(CampaignSeed, 60, corpus));
    }

    [Fact]
    public void Build_HalfTheProgramsAreGeneratedAndHalfAreMutants() {
        IReadOnlyList<PlannedProgram> plan = CampaignPlan.Build(CampaignSeed, 60, Corpus(10));

        Assert.Equal(60, plan.Count);
        Assert.Equal(30, plan.Count(p => p.Kind == CampaignPlan.GeneratedKind));
        Assert.All(plan.Where(p => p.Kind != CampaignPlan.GeneratedKind),
            p => Assert.Contains(p.Kind, Mutator.Operators.Select(o => o.Name)));
    }

    [Fact]
    public void Build_ThePlanAlternates_SoAnyPrefixKeepsTheRatio() {
        IReadOnlyList<PlannedProgram> plan = CampaignPlan.Build(CampaignSeed, 60, Corpus(10));

        Assert.Equal(5, plan.Take(10).Count(p => p.Kind == CampaignPlan.GeneratedKind));
    }

    [Fact]
    public void Build_FewerPrograms_GiveAPrefixOfTheLargerPlan() {
        List<Seed> corpus = Corpus(10);

        IReadOnlyList<PlannedProgram> small = CampaignPlan.Build(CampaignSeed, 20, corpus);
        IReadOnlyList<PlannedProgram> large = CampaignPlan.Build(CampaignSeed, 60, corpus);

        Assert.Equal(small, large.Take(20));
    }

    [Fact]
    public void Build_NoTwoProgramsShareAnIdOrASource() {
        List<Seed> corpus = Corpus(10);
        Dictionary<string, Seed> byId = ById(corpus);

        IReadOnlyList<PlannedProgram> plan = CampaignPlan.Build(CampaignSeed, 80, corpus);

        Assert.Equal(plan.Count, plan.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(plan.Count, plan.Select(p => CampaignPlan.Write(p, CampaignSeed, byId)).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Build_TooFewMutants_FillsThePlanWithGeneratedPrograms() {
        List<Seed> corpus = [new("one@1", SeedKind.Fixture, "one.grob", "x := 1\n")];

        IReadOnlyList<PlannedProgram> plan = CampaignPlan.Build(CampaignSeed, 40, corpus);

        Assert.Equal(40, plan.Count);
        Assert.InRange(plan.Count(p => p.Kind != CampaignPlan.GeneratedKind), 1, 10);
    }

    [Fact]
    public void Build_DifferentCampaignSeeds_GiveDifferentPlans() {
        List<Seed> corpus = Corpus(10);

        Assert.NotEqual(CampaignPlan.Build(1, 40, corpus), CampaignPlan.Build(2, 40, corpus));
    }

    [Fact]
    public void Write_GeneratedProgram_IsTheGeneratorsProgramAtItsIndex() {
        PlannedProgram generated = CampaignPlan.Build(CampaignSeed, 20, Corpus(5)).First(p => p.Kind == CampaignPlan.GeneratedKind);
        int index = int.Parse(generated.Id.Split('/')[^1], System.Globalization.CultureInfo.InvariantCulture);

        string source = CampaignPlan.Write(generated, CampaignSeed, ById([]));

        Assert.Equal(ProgramGenerator.Generate(CampaignSeed, index).Source, source);
        Assert.Equal(ProgramGenerator.Generate(CampaignSeed, index).Id, generated.Id);
    }

    [Fact]
    public void Write_Mutant_IsTheMutatorsProgramForItsSeedOperatorAndDraw() {
        List<Seed> corpus = Corpus(5);
        PlannedProgram mutant = CampaignPlan.Build(CampaignSeed, 20, corpus).First(p => p.Kind != CampaignPlan.GeneratedKind);
        string[] parts = mutant.Id.Split('/');
        Seed seed = corpus.Single(s => s.Id == parts[0]);
        IMutationOperator op = Mutator.Operators.Single(o => o.Name == parts[1]);

        string source = CampaignPlan.Write(mutant, CampaignSeed, ById(corpus));

        Assert.Equal(parts[1], mutant.Kind);
        Assert.Equal(new Mutator(CampaignSeed).Mutate(seed, op, int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture))!.Source, source);
    }

    [Fact]
    public void Write_MutantOfASeedWhoseIdHoldsSlashes_FindsItsSeed() {
        var seed = new Seed("tests/a/b.cs@00ff", SeedKind.RawLiteral, "tests/a/b.cs", "x := 1 + 2\nprint(x)\n");
        PlannedProgram mutant = CampaignPlan.Build(CampaignSeed, 4, [seed]).First(p => p.Kind != CampaignPlan.GeneratedKind);

        string source = CampaignPlan.Write(mutant, CampaignSeed, ById([seed]));

        Assert.StartsWith("tests/a/b.cs@00ff/", mutant.Id, StringComparison.Ordinal);
        Assert.NotEqual(seed.Source, source);
    }

    [Fact]
    public void Write_MutantWhoseSeedIsGone_Throws() {
        var planned = new PlannedProgram("missing@1/swap-operands/0", "swap-operands");

        Assert.Throws<InvalidOperationException>(() => CampaignPlan.Write(planned, CampaignSeed, ById([])));
    }

    [Fact]
    public void Write_MutantItsSeedNoLongerGives_Throws() {
        // A seed with no constant: const-edge has no candidate in it, as after the seed was edited.
        var seed = new Seed("one@1", SeedKind.Fixture, "one.grob", "x := 1\n");
        var planned = new PlannedProgram("one@1/const-edge/0", "const-edge");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CampaignPlan.Write(planned, CampaignSeed, ById([seed])));

        Assert.Equal("one@1/const-edge/0 no longer gives a mutant", error.Message);
    }

    [Fact]
    public void ShardOf_EveryProgram_FallsInOneShardAndTheShardsAreAboutEven() {
        IReadOnlyList<PlannedProgram> plan = CampaignPlan.Build(CampaignSeed, 400, Corpus(20));

        int[] counts = new int[4];
        foreach (PlannedProgram program in plan) {
            int shard = CampaignPlan.ShardOf(program.Id, 4);
            Assert.InRange(shard, 0, 3);
            Assert.Equal(shard, CampaignPlan.ShardOf(program.Id, 4));
            counts[shard]++;
        }

        Assert.All(counts, c => Assert.InRange(c, 60, 140));
    }
}
