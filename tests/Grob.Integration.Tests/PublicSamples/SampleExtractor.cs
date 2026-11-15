using System.Text.RegularExpressions;

using Grob.Integration.Tests.ErrorExamples;

namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// Finds the <c>grob</c> blocks on a page (D-430 Decision 1) and the fences that look like one but
/// would escape extraction (ruling 6's near-miss guard). A block opens at column 0 with exactly
/// three backticks followed by <c>grob</c> and then a space or the end of the line, and closes at
/// the next column-0 line of three backticks. Lines inside any other fence are not scanned, so a
/// page that shows a fence inside a fence does not confuse either walk.
/// </summary>
internal static partial class SampleExtractor {
    private const string CanonicalOpen = "```";

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<fence>`{3,}|~{3,})(?<info>.*)$")]
    private static partial Regex FenceOpenPattern();

    [GeneratedRegex(@"^grob(?: .*)?$")]
    private static partial Regex CanonicalInfoPattern();

    [GeneratedRegex(@"^```\s*$")]
    private static partial Regex CanonicalClosePattern();

    [GeneratedRegex(@"^#{1,6}\s+(?<text>.*)$")]
    private static partial Regex HeadingPattern();

    /// <summary>Every canonical <c>grob</c> block on the page, in order.</summary>
    public static IReadOnlyList<SampleBlock> Extract(string path, string text) => Scan(path, text).Blocks;

    /// <summary>
    /// The fences whose first info-string word is <c>grob</c> in any case, not followed by a letter,
    /// that <see cref="Extract"/> does not take: indented, cased otherwise, tilde-fenced, opened
    /// with four or more backticks or followed by something other than a space. Each is named by
    /// its location.
    /// </summary>
    public static IReadOnlyList<string> NearMisses(string path, string text) => Scan(path, text).NearMisses;

    private static (List<SampleBlock> Blocks, List<string> NearMisses) Scan(string path, string text) {
        var blocks = new List<SampleBlock>();
        var nearMisses = new List<string>();
        string[] lines = ErrorExampleMaster.SplitLines(text);
        string? heading = null;

        int i = 0;
        while (i < lines.Length) {
            i = ScanLine(path, lines, i, ref heading, blocks, nearMisses) + 1;
        }

        return (blocks, nearMisses);
    }

    /// <summary>Reads the line at <paramref name="i"/> and returns the last line it consumed.</summary>
    private static int ScanLine(string path, string[] lines, int i, ref string? heading,
            List<SampleBlock> blocks, List<string> nearMisses) {
        if (HeadingPattern().Match(lines[i]) is { Success: true } h) {
            heading = h.Groups["text"].Value.Trim();
            return i;
        }
        if (FenceOpenPattern().Match(lines[i]) is not { Success: true } open) {
            return i;
        }

        string fence = open.Groups["fence"].Value;
        string info = open.Groups["info"].Value;
        if (open.Groups["indent"].Length == 0 && fence == CanonicalOpen && CanonicalInfoPattern().IsMatch(info)) {
            int end = FindCanonicalClose(lines, i + 1);
            blocks.Add(new SampleBlock(path, i + 1, heading, info[4..].Trim(),
                string.Join('\n', lines[(i + 1)..end]) + "\n"));
            return end;
        }

        if (LooksLikeGrob(info)) {
            nearMisses.Add($"{path}:{i + 1} opens a grob-like fence '{lines[i].Trim()}' that is not extracted; " +
                "open it at column 0 with exactly three backticks and 'grob', or rename the language");
        }
        return FindOtherClose(lines, i + 1, fence);
    }


    private static bool LooksLikeGrob(string info) {
        string word = info.TrimStart().Split([' ', '\t'], 2)[0];
        return word.StartsWith("grob", StringComparison.OrdinalIgnoreCase)
            && (word.Length == 4 || !char.IsLetter(word[4]));
    }

    private static int FindCanonicalClose(string[] lines, int from) {
        int i = from;
        while (i < lines.Length && !CanonicalClosePattern().IsMatch(lines[i])) {
            i++;
        }
        return i;
    }

    /// <summary>
    /// A fence closes at a line of at least as many of the same fence character with nothing else
    /// on it (CommonMark); an indented fence's closer may be indented too.
    /// </summary>
    private static int FindOtherClose(string[] lines, int from, string fence) {
        char mark = fence[0];
        int i = from;
        while (i < lines.Length) {
            string trimmed = lines[i].Trim();
            if (trimmed.Length >= fence.Length && trimmed.All(c => c == mark)) {
                break;
            }
            i++;
        }
        return i;
    }
}
