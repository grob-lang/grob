using Grob.Torture.Generation;
using Grob.Torture.Mutation;

namespace Grob.Torture;

/// <summary>
/// The CI slice (prompt §2 groups 2, 4 and 6): a deterministic run of the seed corpus, a fixed
/// sample of its mutants and a fixed sample of generated programs, judged against the
/// known-findings list. Its programs are fixed by the
/// repository's contents, so two runs over one tree run the same programs (prompt §3, seeds stay
/// fixed).
/// </summary>
internal static class CorpusSlice {
    /// <summary>The known-findings list, repository-relative.</summary>
    public const string KnownFindingsPath = "tests/Grob.Torture.Tests/known-findings.json";

    /// <summary>The document holding the Deferred Work Register, repository-relative.</summary>
    public const string RegisterPath = "docs/design/grob-open-questions.md";

    /// <summary>
    /// The campaign seed of the CI slice's mutants. A constant, since the slice may not change its
    /// programs between runs (prompt §3); a long run draws its own.
    /// </summary>
    public const ulong CampaignSeed = 20261123UL;

    /// <summary>
    /// How many generated programs the slice runs, the first of the generator's at
    /// <see cref="CampaignSeed"/> (decision AA, D1: group 6 sets what the slice carries). They keep
    /// the generator checked against later changes to the checker, the compiler and the VM; a
    /// change to the generator itself changes them, and their known entries must be triaged again.
    /// </summary>
    public const int GeneratedPrograms = 200;

    /// <summary>
    /// What the slice runs: the seeds themselves, then the fixed sample of their mutants
    /// (<see cref="MutantSample"/>), then the first <see cref="GeneratedPrograms"/> generated
    /// programs. All are judged against one known-findings list.
    /// </summary>
    public static IReadOnlyList<Seed> Programs(IReadOnlyList<Seed> seeds) => [
        .. seeds,
        .. MutantSample.Create(seeds, CampaignSeed),
        .. Enumerable.Range(0, GeneratedPrograms).Select(i => ProgramGenerator.Generate(CampaignSeed, i)),
    ];

    /// <summary>
    /// Runs each seed through <paramref name="oracle"/>, in order. A timeout ends the slice at
    /// that program: its worker cannot be killed, so the slice reports it at once rather than
    /// run on beside a spinning thread (gate report §4.4).
    /// </summary>
    public static IReadOnlyList<ProgramResult> Run(IReadOnlyList<Seed> seeds, Oracle oracle) {
        var results = new List<ProgramResult>(seeds.Count);
        foreach (Seed seed in seeds) {
            OracleResult result = oracle.Run(seed.Source, seed.Origin);
            results.Add(new ProgramResult(seed.Id, result.Outcome, result.Findings));
            if (result.TimedOut) break;
        }
        return results;
    }

    /// <summary>Whether a run ended at a timeout, after which no further pass should start.</summary>
    public static bool TimedOut(IReadOnlyList<ProgramResult> results) =>
        results.Count > 0 && results[^1].Findings.Any(f => f.Signature.Class == FindingClass.Timeout);

    /// <summary>Every finding the list does not cover, and every entry that no longer holds.</summary>
    public static SliceVerdict Judge(IReadOnlyList<ProgramResult> results, KnownFindings known) {
        List<(string Program, Finding Finding)> unknown = [.. results.SelectMany(r => r.Findings
            .Where(f => known.RowFor(r.ProgramId, f.Signature) is null)
            .Select(f => (r.ProgramId, f)))];

        Dictionary<string, ProgramResult> byProgram = results.ToDictionary(r => r.ProgramId, StringComparer.Ordinal);
        List<string> stale = [.. known.Entries.SelectMany(e => e.Programs
            .Select(p => (Entry: e, Program: p, Reason: StaleReason(e, p, byProgram)))
            .Where(s => s.Reason is not null)
            .Select(s => $"{s.Entry.Row} {s.Program} {s.Entry.Signature}: {s.Reason}"))];

        return new SliceVerdict(unknown, stale);
    }

    private static string? StaleReason(KnownEntry entry, string program, Dictionary<string, ProgramResult> byProgram) {
        if (!byProgram.TryGetValue(program, out ProgramResult? result)) return "not in the corpus";
        return result.Findings.Any(f => f.Signature.Key == entry.Signature) ? null : "no longer makes this finding";
    }
}
