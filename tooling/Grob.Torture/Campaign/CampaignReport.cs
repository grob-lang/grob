using System.Globalization;
using System.Text;

using Grob.Torture.Findings;
using Grob.Torture.Mutation;

namespace Grob.Torture.Campaign;

/// <summary>
/// The campaign's report, in Markdown: the figures prompt §5 asks for (programs by source and
/// operator, findings by signature), the programs not minimised, the black-box check, and each
/// signature's distinct reproducers, shortest first, which is where triage starts.
/// </summary>
/// <remarks>
/// A signature does not identify a root cause (prompt §1.4), so the report never merges
/// reproducers by signature: it lists the distinct ones under each, with how many programs each
/// came from, and leaves the comparison with each other and with the register to triage.
/// </remarks>
internal static class CampaignReport {
    /// <summary>How many distinct reproducers a signature's section shows.</summary>
    public const int ReproducersShown = 3;

    private const string GeneratedPrefix = "generated/";

    /// <summary>Writes the report.</summary>
    public static string Build(
            CampaignSettings settings,
            IReadOnlyList<ProgramResult> results,
            IReadOnlyList<FindingRecord> records,
            IReadOnlyList<string> unminimised,
            KnownFindings? known,
            IReadOnlyList<BlackBoxResult> blackBox) {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"# Crash oracle campaign\n\nCampaign seed {settings.Seed}, {settings.Programs} programs, {settings.Workers} workers.\n\n");
        Programs(text, results);
        List<string> signatures = Signatures(results, records);
        SignatureTable(text, signatures, results, records, known);
        Unminimised(text, unminimised);
        BlackBoxTable(text, blackBox, records);
        Reproducers(text, signatures, results, records);
        return text.ToString();
    }

    /// <summary>The source of a program: <see cref="CampaignPlan.GeneratedKind"/>, or the operator named in a mutant's id.</summary>
    public static string SourceOf(string programId) {
        if (programId.StartsWith(GeneratedPrefix, StringComparison.Ordinal)) return CampaignPlan.GeneratedKind;
        string[] parts = programId.Split('/');
        return parts.Length >= 3 ? parts[^2] : programId;
    }

    // ---- programs -------------------------------------------------------------------------------

    private static void Programs(StringBuilder text, IReadOnlyList<ProgramResult> results) {
        OutcomeKind[] outcomes = Enum.GetValues<OutcomeKind>();
        text.Append("## Programs\n\n| Source | Programs | ").Append(string.Join(" | ", outcomes)).Append(" |\n|---|---|")
            .Append(string.Concat(outcomes.Select(_ => "---|"))).Append('\n');

        foreach (IGrouping<string, ProgramResult> source in results.GroupBy(r => SourceOf(r.ProgramId)).OrderBy(g => SourceOrder(g.Key)).ThenBy(g => g.Key, StringComparer.Ordinal)) {
            ProgramRow(text, source.Key, [.. source], outcomes);
        }
        ProgramRow(text, "**all**", results, outcomes);
        text.Append('\n');
    }

    private static void ProgramRow(StringBuilder text, string source, IReadOnlyList<ProgramResult> results, OutcomeKind[] outcomes) =>
        text.Append(CultureInfo.InvariantCulture, $"| {source} | {results.Count} | ")
            .Append(string.Join(" | ", outcomes.Select(o => results.Count(r => r.Outcome == o).ToString(CultureInfo.InvariantCulture))))
            .Append(" |\n");

    // Generated programs first, then the operators in the gate report's order.
    private static int SourceOrder(string source) {
        if (source == CampaignPlan.GeneratedKind) return -1;
        int index = Mutator.Operators.Select(o => o.Name).ToList().IndexOf(source);
        return index < 0 ? int.MaxValue : index;
    }

    // ---- signatures ------------------------------------------------------------------------------

    private static List<string> Signatures(IReadOnlyList<ProgramResult> results, IReadOnlyList<FindingRecord> records) =>
        [.. results.SelectMany(r => r.Findings.Select(f => f.Signature.Key)).Concat(records.Select(r => r.Signature))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(s => ProgramsWith(results, s).Count)
            .ThenBy(s => s, StringComparer.Ordinal)];

    private static List<string> ProgramsWith(IReadOnlyList<ProgramResult> results, string signature) =>
        [.. results.Where(r => r.Findings.Any(f => f.Signature.Key == signature)).Select(r => r.ProgramId).Distinct(StringComparer.Ordinal)];

    private static void SignatureTable(StringBuilder text, List<string> signatures, IReadOnlyList<ProgramResult> results,
            IReadOnlyList<FindingRecord> records, KnownFindings? known) {
        int failing = results.Count(r => r.Findings.Count > 0);
        text.Append(CultureInfo.InvariantCulture, $"## Findings by signature\n\n{signatures.Count} signatures over {failing} programs.\n\n")
            .Append("| Signature | Programs | Generated | Mutants | Distinct reproducers | Possibly multi-cause | Known rows |\n")
            .Append("|---|---|---|---|---|---|---|\n");
        foreach (string signature in signatures) {
            List<string> programs = ProgramsWith(results, signature);
            int generated = programs.Count(p => SourceOf(p) == CampaignPlan.GeneratedKind);
            List<FindingRecord> mine = [.. records.Where(r => r.Signature == signature)];
            int distinct = mine.Select(r => r.Reproducer).Distinct(StringComparer.Ordinal).Count();
            text.Append(CultureInfo.InvariantCulture,
                $"| {Cell(signature)} | {programs.Count} | {generated} | {programs.Count - generated} | {distinct} | {mine.Count(r => r.PossiblyMultiCause)} | {KnownRows(known, signature)} |\n");
        }
        text.Append('\n');
    }

    private static string KnownRows(KnownFindings? known, string signature) {
        List<string> rows = [.. (known?.Entries ?? []).Where(e => e.Signature == signature).Select(e => e.Row).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return rows.Count == 0 ? "—" : string.Join(", ", rows);
    }

    // ---- unminimised and black box ---------------------------------------------------------------

    private static void Unminimised(StringBuilder text, IReadOnlyList<string> unminimised) {
        text.Append("## Not minimised\n\n");
        if (unminimised.Count == 0) text.Append("None.\n");
        foreach (string[] line in unminimised.Select(u => u.Split('\t', 2))) {
            text.Append(CultureInfo.InvariantCulture, $"- `{line[0]}`: {line[^1]}\n");
        }
        text.Append('\n');
    }

    private static void BlackBoxTable(StringBuilder text, IReadOnlyList<BlackBoxResult> blackBox, IReadOnlyList<FindingRecord> records) {
        text.Append("## Black-box check\n\n");
        if (blackBox.Count == 0) {
            text.Append("Not run.\n\n");
            return;
        }
        text.Append("| Signature | Verdict | Exit code | Programs |\n|---|---|---|---|\n");
        foreach (IGrouping<(string Signature, BlackBoxVerdict Verdict, int ExitCode), BlackBoxResult> group in blackBox
                .GroupBy(b => (b.Signature, b.Verdict, b.ExitCode))
                .OrderBy(g => g.Key.Signature, StringComparer.Ordinal).ThenBy(g => g.Key.Verdict).ThenBy(g => g.Key.ExitCode)) {
            int programs = records.Count(r => group.Any(b => b.Signature == r.Signature && b.Reproducer == r.Reproducer));
            text.Append(CultureInfo.InvariantCulture, $"| {Cell(group.Key.Signature)} | {group.Key.Verdict} | {group.Key.ExitCode} | {programs} |\n");
        }
        text.Append('\n');
    }

    // ---- reproducers -----------------------------------------------------------------------------

    private static void Reproducers(StringBuilder text, List<string> signatures, IReadOnlyList<ProgramResult> results, IReadOnlyList<FindingRecord> records) {
        text.Append("## Reproducers\n\n");
        foreach (string signature in signatures) {
            List<FindingRecord> mine = [.. records.Where(r => r.Signature == signature)];
            text.Append(CultureInfo.InvariantCulture, $"### {signature}\n\n");
            if (FirstFrame(signature, mine, results) is { } frame) text.Append(CultureInfo.InvariantCulture, $"First frame: `{frame}`.\n\n");

            List<IGrouping<string, FindingRecord>> distinct = [.. mine.GroupBy(r => r.Reproducer, StringComparer.Ordinal)
                .OrderBy(g => g.Key.Length).ThenBy(g => g.Key, StringComparer.Ordinal)];
            foreach (IGrouping<string, FindingRecord> reproducer in distinct.Take(ReproducersShown)) {
                Reproducer(text, reproducer);
            }
            if (distinct.Count > ReproducersShown) {
                text.Append(CultureInfo.InvariantCulture, $"and {distinct.Count - ReproducersShown} more distinct reproducers.\n\n");
            }
        }
    }

    private static void Reproducer(StringBuilder text, IGrouping<string, FindingRecord> reproducer) {
        int count = reproducer.Count();
        string first = reproducer.Select(r => r.ProgramId).Order(StringComparer.Ordinal).First();
        string source = reproducer.Key.EndsWith('\n') ? reproducer.Key : reproducer.Key + "\n";
        text.Append(CultureInfo.InvariantCulture, $"{count} {(count == 1 ? "program" : "programs")}, first `{first}`:\n\n```grob\n{source}```\n\n");
    }

    private static string? FirstFrame(string signature, List<FindingRecord> records, IReadOnlyList<ProgramResult> results) =>
        records.Select(r => r.FirstFrame).FirstOrDefault(f => f is not null)
        ?? results.SelectMany(r => r.Findings).FirstOrDefault(f => f.Signature.Key == signature && f.FirstFrame is not null)?.FirstFrame;

    private static string Cell(string text) => text.Replace("|", "\\|", StringComparison.Ordinal);
}
