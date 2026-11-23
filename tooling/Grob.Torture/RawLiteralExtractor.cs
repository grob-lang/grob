using System.Text.RegularExpressions;

namespace Grob.Torture;

/// <summary>
/// Extracts the multi-line C# raw string literals from a C# source file, for use as seeds (gate
/// report §1.3). An opener is three or more quotes at the end of a line; its closer is the first
/// later line whose first non-blank characters are the same number of quotes and no more. The
/// closer's indentation is removed from every line, which is C#'s own rule. Interpolated
/// literals (<c>$"""</c>) are skipped, since their holes are C# values, not Grob. Roslyn would be
/// exact, but it is not in the package set, and this rule extracted every literal in the test
/// projects with none unterminated.
/// </summary>
internal static partial class RawLiteralExtractor {
    [GeneratedRegex(@"(?<![""$])(?<dollars>\$*)(?<quotes>""{3,})[ \t]*$")]
    private static partial Regex OpenerPattern();

    /// <summary>The value of every non-interpolated multi-line raw literal in <paramref name="csharp"/>, in order.</summary>
    public static IReadOnlyList<string> Extract(string csharp) {
        string[] lines = csharp.ReplaceLineEndings("\n").Split('\n');
        var literals = new List<string>();
        int i = 0;
        while (i < lines.Length) {
            i = ExtractAt(lines, i, literals) + 1;
        }
        return literals;
    }

    // Returns the last line consumed: the closer when a literal opens here, else the line itself.
    private static int ExtractAt(string[] lines, int i, List<string> literals) {
        if (OpenerPattern().Match(lines[i]) is not { Success: true } open || IsInLineComment(lines[i], open.Index)) return i;

        string quotes = open.Groups["quotes"].Value;
        int close = FindCloser(lines, i + 1, quotes);
        if (close < 0) return i;
        if (open.Groups["dollars"].Length == 0) {
            literals.Add(Body(lines, i + 1, close));
        }
        return close;
    }

    // Quotes after a '//' are comment text, not an opener. A '//' inside an ordinary string
    // before the quotes would hide a real opener; no test literal is written so.
    private static bool IsInLineComment(string line, int index) {
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment >= 0 && comment < index;
    }

    private static int FindCloser(string[] lines, int from, string quotes) {
        for (int j = from; j < lines.Length; j++) {
            string trimmed = lines[j].TrimStart(' ', '\t');
            if (trimmed.StartsWith(quotes, StringComparison.Ordinal)
                    && (trimmed.Length == quotes.Length || trimmed[quotes.Length] != '"')) {
                return j;
            }
        }
        return -1;
    }

    private static string Body(string[] lines, int first, int close) {
        string closer = lines[close];
        string indent = closer[..(closer.Length - closer.TrimStart(' ', '\t').Length)];
        return string.Join('\n', lines[first..close].Select(line => line.StartsWith(indent, StringComparison.Ordinal)
            ? line[indent.Length..]
            : line.TrimStart(' ', '\t')));
    }
}
