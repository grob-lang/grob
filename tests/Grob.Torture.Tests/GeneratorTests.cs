using Grob.Torture.Generation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// A generated program is fixed by its campaign seed and index alone (prompt §3, seeds stay
/// fixed): the same pair gives the same text in any run, in any order, and with no other program
/// generated beside it.
/// </summary>
public sealed class GeneratorTests {
    private const ulong CampaignSeed = 20261124UL;

    [Fact]
    public void Generate_SameSeedAndIndex_GiveTheSameProgramTwice() {
        Seed first = ProgramGenerator.Generate(CampaignSeed, 17);
        Seed second = ProgramGenerator.Generate(CampaignSeed, 17);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_OneIndexAlone_MatchesTheSameIndexGeneratedAmongOthers() {
        List<Seed> batch = [.. Enumerable.Range(0, 30).Select(i => ProgramGenerator.Generate(CampaignSeed, i))];

        Seed alone = ProgramGenerator.Generate(CampaignSeed, 23);

        Assert.Equal(batch[23], alone);
    }

    [Fact]
    public void Generate_DifferentIndexes_GiveDifferentPrograms() {
        HashSet<string> sources = [.. Enumerable.Range(0, 50).Select(i => ProgramGenerator.Generate(CampaignSeed, i).Source)];

        Assert.Equal(50, sources.Count);
    }

    [Fact]
    public void Generate_DifferentCampaignSeeds_GiveDifferentPrograms() {
        Assert.NotEqual(ProgramGenerator.Generate(1, 0).Source, ProgramGenerator.Generate(2, 0).Source);
    }

    [Fact]
    public void Generate_Seed_CarriesTheProgramIdKindAndOrigin() {
        Seed seed = ProgramGenerator.Generate(0x1fUL, 5);

        Assert.Equal("generated/000000000000001f/5", seed.Id);
        Assert.Equal(SeedKind.Generated, seed.Kind);
        Assert.Equal(ProgramGenerator.Origin, seed.Origin);
        Assert.EndsWith("\n", seed.Source, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", seed.Source, StringComparison.Ordinal);
    }
}
