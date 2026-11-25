using System.Globalization;

using Grob.Torture.Generation;
using Grob.Torture.Mutation;

namespace Grob.Torture.Campaign;

/// <summary>
/// Which programs a campaign runs (prompt §2 group 6; gate report §1.5): half written by the
/// generator and half mutants of the seed corpus, alternating, so that any prefix of a plan keeps
/// the ratio and a smaller plan of one seed is a prefix of a larger one. A plan names its programs;
/// a worker writes each again from its id, as the CI slice's mutants are written (decision T).
/// </summary>
/// <remarks>
/// No source is planned twice: a mutant with few candidates gives the same program for several
/// draws, and running and minimising it again would only inflate the counts. When the corpus runs
/// out of mutants, the rest of the plan is generated.
/// </remarks>
internal static class CampaignPlan {
    /// <summary>The kind of a generated program.</summary>
    public const string GeneratedKind = "generated";

    /// <summary>
    /// The draws each seed and operator are tried with: four times the CI slice's two, so a seed
    /// with many candidates for an operator has more of them reached.
    /// </summary>
    public const int MutantDraws = 8;

    /// <summary>The plan of <paramref name="programs"/> programs that <paramref name="campaignSeed"/> gives over <paramref name="corpus"/>.</summary>
    public static IReadOnlyList<PlannedProgram> Build(ulong campaignSeed, int programs, IReadOnlyList<Seed> corpus) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        using IEnumerator<PlannedProgram> mutants = Mutants(campaignSeed, corpus, seen).GetEnumerator();
        int nextIndex = 0;

        var plan = new List<PlannedProgram>(programs);
        bool mutantsLeft = true;
        while (plan.Count < programs) {
            bool wantMutant = plan.Count % 2 == 1 && mutantsLeft;
            if (wantMutant && mutants.MoveNext()) {
                plan.Add(mutants.Current);
                continue;
            }
            if (wantMutant) mutantsLeft = false;
            plan.Add(NextGenerated());
        }
        return plan;

        PlannedProgram NextGenerated() {
            while (true) {
                Seed program = ProgramGenerator.Generate(campaignSeed, nextIndex++);
                if (seen.Add(program.Source)) return new PlannedProgram(program.Id, GeneratedKind);
            }
        }
    }

    /// <summary>The worker, of <paramref name="workers"/>, that runs <paramref name="programId"/>.</summary>
    public static int ShardOf(string programId, int workers) =>
        (int)(SeedCorpus.Fnv1a64($"shard|{programId}") % (ulong)workers);

    /// <summary>The source of a planned program, written again from its id.</summary>
    /// <exception cref="InvalidOperationException">A mutant's seed is not in <paramref name="corpus"/>, or no longer gives the mutant.</exception>
    public static string Write(PlannedProgram program, ulong campaignSeed, IReadOnlyDictionary<string, Seed> corpus) {
        if (program.Kind == GeneratedKind) {
            return ProgramGenerator.Generate(campaignSeed, int.Parse(program.Id[(program.Id.LastIndexOf('/') + 1)..], CultureInfo.InvariantCulture)).Source;
        }
        (string seedId, IMutationOperator op, int draw) = ParseMutant(program.Id);
        if (!corpus.TryGetValue(seedId, out Seed? seed)) {
            throw new InvalidOperationException($"the seed of {program.Id} is not in the corpus");
        }
        return new Mutator(campaignSeed).Mutate(seed, op, draw)?.Source
            ?? throw new InvalidOperationException($"{program.Id} no longer gives a mutant");
    }

    // A seed id can hold slashes, so a mutant's id is read from its end.
    private static (string SeedId, IMutationOperator Op, int Draw) ParseMutant(string id) {
        int drawAt = id.LastIndexOf('/');
        int opAt = id.LastIndexOf('/', drawAt - 1);
        string opName = id[(opAt + 1)..drawAt];
        return (id[..opAt], Mutator.Operators.Single(o => o.Name == opName), int.Parse(id[(drawAt + 1)..], CultureInfo.InvariantCulture));
    }

    // Every seed, operator and draw, in an order the campaign seed shuffles, so that a short plan
    // reaches across the corpus and its operators rather than the first seeds alone.
    private static IEnumerable<PlannedProgram> Mutants(ulong campaignSeed, IReadOnlyList<Seed> corpus, HashSet<string> seen) {
        var mutator = new Mutator(campaignSeed);
        IEnumerable<(Seed Seed, IMutationOperator Op, int Draw, string Id)> keys =
            from seed in corpus
            from op in Mutator.Operators
            from draw in Enumerable.Range(0, MutantDraws)
            let id = Mutator.ProgramId(seed.Id, op.Name, draw)
            orderby SeedCorpus.Fnv1a64($"{campaignSeed:x16}|order|{id}"), id
            select (seed, op, draw, id);

        foreach ((Seed seed, IMutationOperator op, int draw, string id) in keys) {
            if (mutator.Mutate(seed, op, draw) is { } mutant && seen.Add(mutant.Source)) {
                yield return new PlannedProgram(id, op.Name);
            }
        }
    }
}
