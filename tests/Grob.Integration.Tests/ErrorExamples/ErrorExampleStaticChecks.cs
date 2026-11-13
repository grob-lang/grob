using System.Text;
using System.Text.RegularExpressions;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The harness's static layer (D-428): what a gold master must satisfy against its own source,
/// the catalogue and the README index, independent of what the CLI prints. A failure here is a
/// defect in the pair, never a quarantine.
/// </summary>
internal static partial class ErrorExampleStaticChecks {
    private const string IndexHeading = "## Code → slug index";

    [GeneratedRegex(@"^// (E\d{4})\b")]
    private static partial Regex SourceHeaderPattern();

    // house-style's hedge words; `just` and `straightforward` are banned only when they
    // characterise difficulty, which a pattern cannot tell, so they are not checked.
    [GeneratedRegex(@"\b(simply|obviously|merely|of course)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HedgePattern();

    [GeneratedRegex(@"^\| (E\d{4}) \| `([^`]+)` \| [^|]+\|$", RegexOptions.Multiline)]
    private static partial Regex IndexRowPattern();

    /// <summary>Every static problem in one pair, or none.</summary>
    public static IReadOnlyList<string> CheckPair(string slug, string source, string masterText,
            IReadOnlyDictionary<string, string> catalogueTitles) {
        var problems = new List<string>();
        string master = masterText.Replace("\r\n", "\n", StringComparison.Ordinal);
        string[] sourceLines = ErrorExampleMaster.SplitLines(source);

        CheckText(master, problems);

        ErrorExampleMaster parsed = ErrorExampleMaster.Parse(master);
        problems.AddRange(parsed.FormatProblems.Select(p => "master " + p));
        if (parsed.Diagnostics.Count == 0) {
            problems.Add("master: no header");
            return problems;
        }

        foreach (MasterDiagnostic diagnostic in parsed.Diagnostics) {
            CheckDiagnostic(slug, diagnostic, sourceLines, catalogueTitles, problems);
        }

        CheckSourceHeader(sourceLines, parsed.Diagnostics[0].Code, problems);
        return problems;
    }

    /// <summary>The README's code-to-slug index against the pair directories and their first codes.</summary>
    public static IReadOnlyList<string> CheckIndex(string readme, IReadOnlyDictionary<string, string?> firstCodeBySlug) {
        string text = readme.Replace("\r\n", "\n", StringComparison.Ordinal);
        int start = text.IndexOf(IndexHeading, StringComparison.Ordinal);
        if (start < 0) return ["README has no Code → slug index section"];

        string section = text[start..];
        int end = section.IndexOf("\n-----", StringComparison.Ordinal);
        if (end >= 0) section = section[..end];

        var problems = new List<string>();
        var indexed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match row in IndexRowPattern().Matches(section)) {
            string code = row.Groups[1].Value;
            string slug = row.Groups[2].Value;
            if (!indexed.TryAdd(slug, code)) {
                problems.Add($"index lists `{slug}` twice");
            } else if (!firstCodeBySlug.TryGetValue(slug, out string? firstCode)) {
                problems.Add($"index row `{slug}` has no directory");
            } else if (firstCode != code) {
                problems.Add($"index gives `{slug}` {code}; its master's first header is {firstCode}");
            }
        }

        problems.AddRange(firstCodeBySlug.Keys
            .Where(slug => !indexed.ContainsKey(slug))
            .Order(StringComparer.Ordinal)
            .Select(slug => $"directory `{slug}` has no index row"));
        return problems;
    }

    private static void CheckText(string master, List<string> problems) {
        if (!master.EndsWith('\n') || master.EndsWith("\n\n", StringComparison.Ordinal)) {
            problems.Add("master: not exactly one trailing newline");
        }

        if (master.Contains('\u001b')) problems.Add("master: ANSI escape");

        problems.AddRange(master.EnumerateRunes()
            .Where(IsPictograph)
            .Select(rune => $"master: pictograph U+{rune.Value:X4}"));
        problems.AddRange(HedgePattern().Matches(master).Select(m => $"master: hedge word '{m.Value}'"));
    }

    // Pictographs, not typography: the arrows block stays legal (a master uses U+2192).
    private static bool IsPictograph(Rune rune) =>
        rune.Value is (>= 0x2600 and <= 0x27BF) or 0xFE0F or 0x200D or (>= 0x1F000 and <= 0x1FAFF);

    private static void CheckDiagnostic(string slug, MasterDiagnostic diagnostic, string[] sourceLines,
            IReadOnlyDictionary<string, string> catalogueTitles, List<string> problems) {
        string code = diagnostic.Code;
        if (!catalogueTitles.TryGetValue(code, out string? title)) {
            problems.Add($"{code}: not in ErrorCatalog");
        } else if (title != diagnostic.Title) {
            problems.Add($"{code}: title '{diagnostic.Title}' is not the catalogue's '{title}'");
        }

        foreach (MasterSnippetLine snippet in diagnostic.Snippets) {
            string? actual = SourceLine(sourceLines, snippet.LineNumber);
            if (actual is null) {
                problems.Add($"{code}: snippet line {snippet.LineNumber} '{snippet.Text}' is not a line of the source");
            } else if (actual != snippet.Text) {
                problems.Add($"{code}: snippet line {snippet.LineNumber} '{snippet.Text}' is not source line {snippet.LineNumber} '{actual}'");
            }
        }

        if (diagnostic.Location is not { } location) {
            problems.Add($"{code}: no location");
            return;
        }

        if (location.File != $"{slug}.grob") {
            problems.Add($"{code}: location file '{location.File}' is not '{slug}.grob'");
        }

        if (location.Column is null) problems.Add($"{code}: location {location.Line} has no column");

        CheckCaret(code, location, diagnostic.FirstCaret, sourceLines, problems);
    }

    private static void CheckCaret(string code, MasterLocation location, MasterCaret? caret, string[] sourceLines,
            List<string> problems) {
        if (caret is null) {
            problems.Add($"{code}: no caret");
            return;
        }

        if (caret.LineNumber != location.Line) {
            problems.Add($"{code}: first caret is under line {caret.LineNumber}, location says line {location.Line}");
        }

        if (location.Column is { } column && column != caret.Column) {
            problems.Add($"{code}: caret at column {caret.Column}, location says {column}");
        }

        string line = SourceLine(sourceLines, caret.LineNumber) ?? string.Empty;
        if (caret.Column < 1 || caret.Column > line.Length || char.IsWhiteSpace(line[caret.Column - 1])) {
            problems.Add($"{code}: caret column {caret.Column} is on whitespace or past the end of line {caret.LineNumber}");
        }
    }

    private static void CheckSourceHeader(string[] sourceLines, string firstCode, List<string> problems) {
        Match header = SourceHeaderPattern().Match(sourceLines.Length > 0 ? sourceLines[0] : string.Empty);
        if (!header.Success) {
            problems.Add("source: first line is not a // Exxxx comment");
        } else if (header.Groups[1].Value != firstCode) {
            problems.Add($"source: header comment names {header.Groups[1].Value}, the master's first header {firstCode}");
        }
    }

    private static string? SourceLine(string[] sourceLines, int lineNumber) =>
        lineNumber >= 1 && lineNumber <= sourceLines.Length ? sourceLines[lineNumber - 1] : null;
}
