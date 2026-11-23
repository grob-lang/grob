using Grob.Torture.Mutation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The CI slice's mutants are a fixed sample: the same programs every run, and a seed's mutants do
/// not change when another seed is added (prompt §3).
/// </summary>
public sealed class MutantSampleTests {
    private const ulong CampaignSeed = 99;

    private static Seed MakeSeed(int number) => new($"seed-{number}@{number:x16}", SeedKind.Fixture, $"seed-{number}.grob",
        $"fn f{number}(a: int, b: int): int {{\n    return a + b\n}}\ns := \"ab\"\nn: string? := nil\nx := f{number}(1, 2)\nprint(s.upper())\nconst k := 10 / 2\n");

    private static List<Seed> Seeds(int count) => [.. Enumerable.Range(0, count).Select(MakeSeed)];

    [Fact]
    public void Create_SameSeedsAndCampaignSeed_GiveTheSameProgramsTwice() {
        List<Seed> seeds = Seeds(60);

        IReadOnlyList<Seed> first = MutantSample.Create(seeds, CampaignSeed);
        IReadOnlyList<Seed> second = MutantSample.Create(seeds, CampaignSeed);

        Assert.NotEmpty(first);
        Assert.Equal(first.Select(s => (s.Id, s.Source)), second.Select(s => (s.Id, s.Source)));
    }

    [Fact]
    public void Create_AddingASeed_LeavesEveryOtherSeedsMutantsAlone() {
        List<Seed> seeds = Seeds(60);
        IReadOnlyList<Seed> before = MutantSample.Create(seeds, CampaignSeed);

        seeds.Insert(30, MakeSeed(1000));
        IReadOnlyList<Seed> after = MutantSample.Create(seeds, CampaignSeed);

        Assert.Equal(before.Select(s => s.Id), after.Where(s => !s.Id.StartsWith("seed-1000@", StringComparison.Ordinal)).Select(s => s.Id));
    }

    [Fact]
    public void Create_ProgramIds_NameTheirSeedAnOperatorAndADraw() {
        IReadOnlyList<Seed> sample = MutantSample.Create(Seeds(60), CampaignSeed);

        Assert.All(sample, s => {
            string[] parts = s.Id.Split('/');
            Assert.Equal(3, parts.Length);
            Assert.StartsWith("seed-", parts[0], StringComparison.Ordinal);
            Assert.Contains(parts[1], Mutator.Operators.Select(o => o.Name));
            Assert.InRange(int.Parse(parts[2]), 0, MutantSample.DrawsPerOperator - 1);
        });
        Assert.Equal(sample.Count, sample.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Create_ManySeeds_ReachEveryOperator() {
        IReadOnlyList<Seed> sample = MutantSample.Create(Seeds(200), CampaignSeed);

        List<string> reached = [.. sample.Select(s => s.Id.Split('/')[1]).Distinct()];

        Assert.Equal(Mutator.Operators.Count, reached.Count);
    }

    [Fact]
    public void Create_DifferentCampaignSeeds_GiveDifferentSamples() {
        List<Seed> seeds = Seeds(60);

        IReadOnlyList<string> one = [.. MutantSample.Create(seeds, 1).Select(s => s.Id)];
        IReadOnlyList<string> two = [.. MutantSample.Create(seeds, 2).Select(s => s.Id)];

        Assert.NotEqual(one, two);
    }

    [Fact]
    public void Create_EachMutant_DiffersFromItsSeed() {
        List<Seed> seeds = Seeds(60);
        Dictionary<string, string> sources = seeds.ToDictionary(s => s.Id, s => s.Source, StringComparer.Ordinal);

        IReadOnlyList<Seed> sample = MutantSample.Create(seeds, CampaignSeed);

        Assert.All(sample, s => Assert.NotEqual(sources[s.Id.Split('/')[0]], s.Source));
    }

    [Fact]
    public void Picked_IsAboutOneInThirtyTwo() {
        int picked = Enumerable.Range(0, 32000).Count(i => MutantSample.Picked(CampaignSeed, "replace-value", $"p{i}/op/0"));

        Assert.InRange(picked, 800, 1200);
    }

    [Theory]
    [InlineData("const-edge")]
    [InlineData("nullable-receiver")]
    public void Picked_RareOperator_IsAboutOneInFour(string operatorName) {
        int picked = Enumerable.Range(0, 4000).Count(i => MutantSample.Picked(CampaignSeed, operatorName, $"p{i}/op/0"));

        Assert.InRange(picked, 800, 1200);
    }
}
