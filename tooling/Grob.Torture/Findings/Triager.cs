using Grob.Torture.Minimisation;

namespace Grob.Torture.Findings;

/// <summary>
/// Takes a failing program from a finding to a record: minimise it against each of its findings'
/// signatures, and store the original beside the reproducer. Every failing program is minimised and
/// stored before any is set aside as a duplicate (prompt §2 group 6), so a record is never skipped
/// because its signature was seen.
/// </summary>
/// <param name="run">Runs a candidate through the pipeline and returns what it found; the oracle in practice.</param>
/// <param name="store">Where the records go.</param>
internal sealed class Triager(Func<string, IReadOnlyList<Finding>> run, FindingStore store) {
    /// <summary>
    /// Minimises <paramref name="source"/> against each finding's signature and stores one record
    /// per finding.
    /// </summary>
    /// <returns>The minimisation of each finding and whether its record was new.</returns>
    public IReadOnlyList<(Finding Finding, MinimisationResult Minimisation, bool Stored)> Triage(
            string programId, string source, IReadOnlyList<Finding> findings) {
        var outcomes = new List<(Finding, MinimisationResult, bool)>();
        foreach (Finding finding in findings) {
            string key = finding.Signature.Key;
            var minimiser = new Minimiser(candidate => run(candidate).Any(f => f.Signature.Key == key));
            MinimisationResult result = minimiser.Minimise(source);
            bool stored = store.Add(new FindingRecord(
                programId, key, finding.Message, finding.FirstFrame, source, result.Reproducer, result.PossiblyMultiCause));
            outcomes.Add((finding, result, stored));
        }
        return outcomes;
    }
}
