using System.Text.RegularExpressions;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>One quarantined pair: why it fails, what blocks it and exactly how it fails today.</summary>
internal sealed record QuarantineRow(int LineNumber, string Slug, string Category, IReadOnlyList<string> BlockedBy,
    string Reason, IReadOnlyList<ExampleDiagnostic> Observed);

/// <summary>
/// <c>docs/errors/examples/quarantine.md</c> (D-428 Decision 4): prose, then one five-column table.
/// The table runs from its header row to the first line that does not start with <c>|</c>. Every
/// problem names its line; a manifest with any problem fails the harness.
/// </summary>
internal sealed partial class QuarantineManifest {
    public const string FileName = "quarantine.md";

    private const string HeaderRow = "| Pair | Category | Blocked by | Reason | Observed |";
    private const string SeparatorRow = "|---|---|---|---|---|";

    private static readonly HashSet<string> Categories = new(["unbuilt", "gap", "divergent", "stale"], StringComparer.Ordinal);

    private QuarantineManifest(IReadOnlyDictionary<string, QuarantineRow> rows, IReadOnlyList<string> problems) {
        Rows = rows;
        Problems = problems;
    }

    public IReadOnlyDictionary<string, QuarantineRow> Rows { get; }

    public IReadOnlyList<string> Problems { get; }

    [GeneratedRegex(@"^`([a-z0-9]+(?:-[a-z0-9]+)*)`$")]
    private static partial Regex PairPattern();

    [GeneratedRegex(@"^R-\d{2}$")]
    private static partial Regex RegisterItemPattern();

    [GeneratedRegex(@"^`([a-z][A-Za-z0-9]*)`$")]
    private static partial Regex NamespacePattern();

    [GeneratedRegex(@"^Sprint \d+$")]
    private static partial Regex SprintPattern();

    public static QuarantineManifest Parse(string text, IReadOnlySet<string> slugs, DeferredWorkRegister register,
            Func<string, bool> isRegisteredNamespace) {
        var rows = new Dictionary<string, QuarantineRow>(StringComparer.Ordinal);
        var problems = new List<string>();
        string[] lines = ErrorExampleMaster.SplitLines(text);

        int header = Array.IndexOf(lines, HeaderRow);
        if (header < 0 || header + 1 >= lines.Length || lines[header + 1] != SeparatorRow) {
            problems.Add($"{FileName}: expected the header row \"{HeaderRow}\"");
            return new QuarantineManifest(rows, problems);
        }

        var context = new RowContext(slugs, register, isRegisteredNamespace, rows);
        for (int i = header + 2; i < lines.Length && lines[i].StartsWith('|'); i++) {
            var row = new RowReader(i + 1, problems);
            if (row.Read(lines[i], context) is { } parsed) rows.Add(parsed.Slug, parsed);
        }

        return new QuarantineManifest(rows, problems);
    }

    private sealed record RowContext(IReadOnlySet<string> Slugs, DeferredWorkRegister Register,
        Func<string, bool> IsRegisteredNamespace, IReadOnlyDictionary<string, QuarantineRow> Seen);

    /// <summary>Reads one table row, adding a problem prefixed with its line for every bad cell.</summary>
    private sealed class RowReader(int lineNumber, List<string> problems) {
        private bool _ok = true;

        private void Fail(string message) {
            problems.Add($"{FileName}:{lineNumber}: {message}");
            _ok = false;
        }

        public QuarantineRow? Read(string line, RowContext context) {
            string[] cells = line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length != 5) {
                Fail($"row has {cells.Length} cells, expected 5");
                return null;
            }

            string? slug = ReadPair(cells[0], context);
            if (!Categories.Contains(cells[1])) Fail($"category `{cells[1]}` is not one of unbuilt, gap, divergent or stale");
            IReadOnlyList<string> blockedBy = ReadBlockedBy(cells[2], slug ?? "?", context);
            if (cells[3].Length == 0) Fail("Reason is empty");
            IReadOnlyList<ExampleDiagnostic> observed = ReadObserved(cells[4], slug);

            return _ok && slug is not null
                ? new QuarantineRow(lineNumber, slug, cells[1], blockedBy, cells[3], observed)
                : null;
        }

        private string? ReadPair(string cell, RowContext context) {
            Match pair = PairPattern().Match(cell);
            if (!pair.Success) {
                Fail($"pair cell `{cell}` is not a backticked slug");
                return null;
            }

            string slug = pair.Groups[1].Value;
            if (!context.Slugs.Contains(slug)) {
                Fail($"pair `{slug}` has no directory under docs/errors/examples/");
                return null;
            }

            if (context.Seen.TryGetValue(slug, out QuarantineRow? earlier)) {
                Fail($"pair `{slug}` is already quarantined on line {earlier.LineNumber}");
                return null;
            }

            return slug;
        }

        private List<string> ReadBlockedBy(string cell, string slug, RowContext context) {
            if (cell.Length == 0) {
                Fail("Blocked by is empty");
                return [];
            }

            List<string> items = [.. cell.Split(", ")];
            foreach (string item in items) CheckBlocker(item, slug, context);
            return items;
        }

        private void CheckBlocker(string item, string slug, RowContext context) {
            if (RegisterItemPattern().IsMatch(item)) {
                if (context.Register.ClosedBy.TryGetValue(item, out string? decision)) {
                    Fail($"{item} is closed (by {decision}); re-classify pair `{slug}` or remove its row");
                } else if (!context.Register.Open.Contains(item)) {
                    Fail($"{item} is not a row of the Deferred Work Register");
                }
            } else if (NamespacePattern().Match(item) is { Success: true } ns) {
                if (context.IsRegisteredNamespace(ns.Groups[1].Value)) {
                    Fail($"namespace `{ns.Groups[1].Value}` is registered in the stdlib catalogue; re-classify pair `{slug}`");
                }
            } else if (!SprintPattern().IsMatch(item)) {
                // A Sprint N blocker is checked by strictness alone.
                Fail($"Blocked by item `{item}` is not an R-## register item, a `namespace` or Sprint N");
            }
        }

        private IReadOnlyList<ExampleDiagnostic> ReadObserved(string cell, string? slug) {
            if (cell.Length == 0) {
                Fail("Observed is empty; write `none` for a pair that produces no diagnostics");
                return [];
            }

            if (ExampleDiagnostic.TryDecodeList(cell, $"{slug}.grob", out IReadOnlyList<ExampleDiagnostic>? observed, out string? bad)) {
                return observed;
            }

            Fail($"Observed entry `{bad}` is not severity[Exxxx] [file:]line[:column]");
            return [];
        }
    }
}

/// <summary>
/// The verdict for one pair (D-428 Decision 4). An unquarantined pair must match its master. A
/// quarantined pair must fail exactly as its <em>Observed</em> cell records: one that now matches
/// must leave the manifest, and one that fails differently is a regression behind a blocker that
/// no longer describes it.
/// </summary>
internal static class PairVerdict {
    /// <summary>Null when the pair is as the gate requires, otherwise why it is not.</summary>
    public static string? Check(string slug, IReadOnlyList<ExampleDiagnostic> expected,
            IReadOnlyList<ExampleDiagnostic> actual, QuarantineRow? row) {
        string file = $"{slug}.grob";
        string? mismatch = ExampleDiagnostic.Compare(expected, actual, file);
        if (row is null) return mismatch;

        if (mismatch is null) {
            return $"pair `{slug}` is quarantined but now matches its master; remove its row from {QuarantineManifest.FileName}";
        }

        return row.Observed.SequenceEqual(actual)
            ? null
            : $"pair `{slug}` no longer fails as {QuarantineManifest.FileName} records\n" +
              $"recorded: {ExampleDiagnostic.EncodeList(row.Observed, file)}\n" +
              $"actual:   {ExampleDiagnostic.EncodeList(actual, file)}";
    }
}
