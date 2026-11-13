using System.Text.RegularExpressions;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>A master's <c>--&gt;</c> location. <see cref="Column"/> is null when the line gives none.</summary>
internal sealed record MasterLocation(string File, int Line, int? Column);

/// <summary>A numbered snippet line: the source line number it claims and the text it shows.</summary>
internal sealed record MasterSnippetLine(int LineNumber, string Text);

/// <summary>
/// The first caret under a diagnostic: the snippet line it sits beneath and the 1-based source
/// column its first <c>^</c> marks.
/// </summary>
internal sealed record MasterCaret(int LineNumber, int Column);

/// <summary>One diagnostic of a master: its header, first location, snippet lines and first caret.</summary>
internal sealed class MasterDiagnostic(string severity, string code, string title) {
    public string Severity { get; } = severity;

    public string Code { get; } = code;

    public string Title { get; } = title;

    public MasterLocation? Location { get; set; }

    public List<MasterSnippetLine> Snippets { get; } = [];

    public MasterCaret? FirstCaret { get; set; }
}

/// <summary>
/// A parsed gold master (<c>&lt;slug&gt;.expected.txt</c>), read against the line grammar D-428's
/// harness fixes. Note, help and stack-chain lines are not read: they are R-25's to compare.
/// <see cref="FormatProblems"/> holds every line that claims a form and does not match it.
/// </summary>
internal sealed partial class ErrorExampleMaster {
    private ErrorExampleMaster(IReadOnlyList<MasterDiagnostic> diagnostics, IReadOnlyList<string> formatProblems) {
        Diagnostics = diagnostics;
        FormatProblems = formatProblems;
    }

    public IReadOnlyList<MasterDiagnostic> Diagnostics { get; }

    public IReadOnlyList<string> FormatProblems { get; }

    [GeneratedRegex(@"^(error|warning)\[(E\d{4})\]: (.+)$")]
    private static partial Regex HeaderPattern();

    // A bare file name has no colon; a greedy \S+ would take "file:line" as the file (F1).
    [GeneratedRegex(@"^  --> ([^\s:]+):(\d+)(?::(\d+))?$")]
    private static partial Regex LocationPattern();

    [GeneratedRegex(@"^ *(\d+) \|(?: (.*))?$")]
    private static partial Regex SnippetPattern();

    [GeneratedRegex(@"^ *\| *\^")]
    private static partial Regex CaretPattern();

    /// <summary>Splits text into lines, tolerating CRLF, without the empty piece after a final newline.</summary>
    internal static string[] SplitLines(string text) {
        string normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (normalised.EndsWith('\n')) normalised = normalised[..^1];
        return normalised.Length == 0 ? [] : normalised.Split('\n');
    }

    public static ErrorExampleMaster Parse(string text) {
        var diagnostics = new List<MasterDiagnostic>();
        var problems = new List<string>();
        MasterDiagnostic? current = null;
        (int LineNumber, int TextStart)? lastSnippet = null;

        string[] lines = SplitLines(text);
        for (int i = 0; i < lines.Length; i++) {
            string line = lines[i];
            int lineNumber = i + 1;

            if (line.StartsWith("error", StringComparison.Ordinal) || line.StartsWith("warning", StringComparison.Ordinal)) {
                current = ReadHeader(line, lineNumber, problems);
                if (current is not null) diagnostics.Add(current);
                lastSnippet = null;
            } else if (line.TrimStart().StartsWith("-->", StringComparison.Ordinal)) {
                ReadLocation(line, lineNumber, current, problems);
            } else if (SnippetPattern().Match(line) is { Success: true } snippet) {
                int number = ParseInt(snippet.Groups[1].Value);
                current?.Snippets.Add(new MasterSnippetLine(number, snippet.Groups[2].Value));
                lastSnippet = (number, line.IndexOf('|') + 2);
            } else if (CaretPattern().IsMatch(line) && current is { FirstCaret: null }) {
                ReadCaret(line, lineNumber, current, lastSnippet, problems);
            }
        }

        return new ErrorExampleMaster(diagnostics, problems);
    }

    private static MasterDiagnostic? ReadHeader(string line, int lineNumber, List<string> problems) {
        Match header = HeaderPattern().Match(line);
        if (header.Success) return new MasterDiagnostic(header.Groups[1].Value, header.Groups[2].Value, header.Groups[3].Value);

        problems.Add($"line {lineNumber}: malformed header '{line}'");
        return null;
    }

    private static void ReadLocation(string line, int lineNumber, MasterDiagnostic? current, List<string> problems) {
        Match location = LocationPattern().Match(line);
        if (!location.Success) {
            problems.Add($"line {lineNumber}: malformed location '{line}'");
            return;
        }

        if (current is null || current.Location is not null) return;
        int? column = location.Groups[3].Success ? ParseInt(location.Groups[3].Value) : null;
        current.Location = new MasterLocation(location.Groups[1].Value, ParseInt(location.Groups[2].Value), column);
    }

    private static void ReadCaret(string line, int lineNumber, MasterDiagnostic current,
            (int LineNumber, int TextStart)? lastSnippet, List<string> problems) {
        if (lastSnippet is not { } snippet) {
            problems.Add($"line {lineNumber}: caret with no snippet line above it");
            return;
        }

        current.FirstCaret = new MasterCaret(snippet.LineNumber, line.IndexOf('^') - snippet.TextStart + 1);
    }

    private static int ParseInt(string digits) => int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
}
