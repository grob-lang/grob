using System.Text.RegularExpressions;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The Deferred Work Register in <c>docs/design/grob-open-questions.md</c>, read for the quarantine
/// manifest's guard: an <c>R-##</c> row between <c>### Open items</c> and <c>### Closed items</c> is
/// open; one in the closed table is closed by the decision its <em>Closed by</em> cell names. A row
/// in neither does not exist.
/// </summary>
internal sealed partial record DeferredWorkRegister(IReadOnlySet<string> Open, IReadOnlyDictionary<string, string> ClosedBy) {
    private const string OpenHeading = "### Open items";
    private const string ClosedHeading = "### Closed items";

    [GeneratedRegex(@"^\| (R-\d{2}) \|")]
    private static partial Regex RowPattern();

    [GeneratedRegex(@"\| \*\*(D-\d{3})\*\* \|")]
    private static partial Regex ClosedByPattern();

    public static DeferredWorkRegister Parse(string text) {
        var open = new HashSet<string>(StringComparer.Ordinal);
        var closed = new Dictionary<string, string>(StringComparer.Ordinal);
        string? section = null;

        foreach (string line in ErrorExampleMaster.SplitLines(text)) {
            if (line.StartsWith("### ", StringComparison.Ordinal)) {
                section = line is OpenHeading or ClosedHeading ? line : null;
            } else if (section is not null && RowPattern().Match(line) is { Success: true } row) {
                if (section == OpenHeading) {
                    open.Add(row.Groups[1].Value);
                } else {
                    Match closedBy = ClosedByPattern().Match(line);
                    closed[row.Groups[1].Value] = closedBy.Success ? closedBy.Groups[1].Value : "an unnamed decision";
                }
            }
        }

        return new DeferredWorkRegister(open, closed);
    }
}
