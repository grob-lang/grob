namespace Grob.Torture.Mutation;

/// <summary>
/// The fixed sample of mutants the CI slice runs (prompt §2 group 4; §3, seeds stay fixed). A
/// program is in the sample when a hash of its id and the campaign seed says so, so a seed's
/// mutants do not depend on any other seed: adding a fixture or a test with a raw literal adds
/// programs and never reshuffles the existing ones, and the known-findings list stays valid for
/// every seed whose text is unchanged. Choosing seeds by position would renumber the whole sample
/// each time the corpus grew.
/// </summary>
/// <remarks>
/// The sample is about 550 programs, not the gate report's 3,000. A mutant costs a few milliseconds
/// and a bounded or regex-heavy one a second, so the sample was sized to keep the slice inside its
/// budget, and the triage of what it finds to a size a pull request can carry. The long mode runs
/// the many (gate report §1.5).
/// </remarks>
internal static class MutantSample {
    /// <summary>The draws each seed and operator are tried with.</summary>
    public const int DrawsPerOperator = 2;

    /// <summary>A program is in the sample one time in this many, for an operator with candidates in most seeds.</summary>
    public const ulong OneIn = 32;

    /// <summary>
    /// A program of a rarer operator is in the sample one time in this many. Measured at the
    /// corpus's size, <c>const-edge</c> has a candidate in 57 seeds and <c>nullable-receiver</c> in
    /// 68, against hundreds for every other operator, so at <see cref="OneIn"/> a sample could miss
    /// either. A rate by operator is fixed in the code, never computed from the corpus, since a
    /// computed rate would reshuffle the sample as the corpus grew.
    /// </summary>
    public const ulong RareOneIn = 4;

    private static readonly HashSet<string> RareOperators = new(["const-edge", "nullable-receiver"], StringComparer.Ordinal);

    /// <summary>
    /// The mutants of <paramref name="seeds"/>, in seed, operator and draw order, as seeds the
    /// oracle can run: each takes the program id as its id and keeps its seed's kind and origin.
    /// </summary>
    public static IReadOnlyList<Seed> Create(IReadOnlyList<Seed> seeds, ulong campaignSeed) {
        var mutator = new Mutator(campaignSeed);
        var sample = new List<Seed>();
        foreach (Seed seed in seeds) {
            foreach (IMutationOperator mutationOperator in Mutator.Operators) {
                for (int draw = 0; draw < DrawsPerOperator; draw++) {
                    string programId = Mutator.ProgramId(seed.Id, mutationOperator.Name, draw);
                    if (!Picked(campaignSeed, mutationOperator.Name, programId)) continue;
                    if (mutator.Mutate(seed, mutationOperator, draw) is { } mutant) {
                        sample.Add(new Seed(mutant.ProgramId, seed.Kind, seed.Origin, mutant.Source));
                    }
                }
            }
        }
        return sample;
    }

    /// <summary>Whether the program, made by <paramref name="operatorName"/>, is in the sample.</summary>
    public static bool Picked(ulong campaignSeed, string operatorName, string programId) =>
        SeedCorpus.Fnv1a64($"{campaignSeed:x16}|pick|{programId}") % (RareOperators.Contains(operatorName) ? RareOneIn : OneIn) == 0;
}
