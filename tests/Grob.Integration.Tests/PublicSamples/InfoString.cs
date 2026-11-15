using System.Text.RegularExpressions;

using Grob.Integration.Tests.ErrorExamples;

namespace Grob.Integration.Tests.PublicSamples;

/// <summary>What a block's info string declares, and every way it is malformed.</summary>
/// <param name="Fragment">The block is a fragment (D-430 Decision 2).</param>
/// <param name="BlockedBy">The register rows the block is blocked by, in the order written.</param>
/// <param name="Problems">Each rejection, in the gate report §1.5's words.</param>
internal sealed record SampleMarkers(bool Fragment, IReadOnlyList<string> BlockedBy, IReadOnlyList<string> Problems);

/// <summary>
/// The info-string grammar after <c>grob</c> (gate report §1.5): zero or more space-separated
/// tokens, each <c>fragment</c> (at most once) or <c>blocked-by=R-##</c> (two or more digits, each
/// row once, every row open in the Deferred Work Register). Tokens are case-sensitive and
/// order-free.
/// </summary>
internal static partial class InfoString {
    private const string FragmentToken = "fragment";
    private const string BlockerPrefix = "blocked-by=";

    [GeneratedRegex(@"^R-\d{2,}$")]
    private static partial Regex RowPattern();

    public static SampleMarkers Parse(string info, DeferredWorkRegister register) {
        bool fragment = false;
        var rows = new List<string>();
        var problems = new List<string>();

        foreach (string token in info.Split(' ', StringSplitOptions.RemoveEmptyEntries)) {
            if (token == FragmentToken) {
                if (fragment) problems.Add("'fragment' appears more than once in the info string");
                fragment = true;
            } else if (token.StartsWith(BlockerPrefix, StringComparison.Ordinal)) {
                AddBlocker(token, rows, problems, register);
            } else {
                problems.Add($"unknown info-string token '{token}'; a grob fence takes only 'fragment' and 'blocked-by=R-##'");
            }
        }

        return new SampleMarkers(fragment, rows, problems);
    }

    private static void AddBlocker(string token, List<string> rows, List<string> problems, DeferredWorkRegister register) {
        string row = token[BlockerPrefix.Length..];
        if (!RowPattern().IsMatch(row)) {
            problems.Add($"'{token}' is not a blocker; write 'blocked-by=R-' followed by two or more digits");
        } else if (rows.Contains(row)) {
            problems.Add($"'{row}' is named more than once in the info string");
        } else {
            rows.Add(row);
            if (register.ClosedBy.TryGetValue(row, out string? decision)) {
                problems.Add($"{row} is closed (by {decision}); re-classify this block or remove the token");
            } else if (!register.Open.Contains(row)) {
                problems.Add($"{row} is not a row of the Deferred Work Register");
            }
        }
    }
}
