namespace Grob.Torture;

/// <summary>A slice's results judged against the known-findings list.</summary>
/// <param name="Unknown">Every finding no entry covers, with its program.</param>
/// <param name="Stale">Every entry's program that is not in the run or no longer makes the entry's finding.</param>
internal sealed record SliceVerdict(IReadOnlyList<(string Program, Finding Finding)> Unknown, IReadOnlyList<string> Stale);
