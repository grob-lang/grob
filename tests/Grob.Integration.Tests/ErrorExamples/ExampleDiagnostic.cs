using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// One diagnostic as the behavioural layer compares it (D-428 Decision 2): severity, code, the
/// file by bare name, the start line and the start column. <see cref="Column"/> is null when the
/// location gives none; it never acquires one, so it fails against a master that has one.
/// </summary>
internal sealed partial record ExampleDiagnostic(string Severity, string Code, string File, int Line, int? Column) {
    private const string None = "none";
    private const string Separator = "; ";

    // A file holds at least one non-digit, so "3:12" is line 3, column 12 — never file "3", line 12.
    [GeneratedRegex(@"^(error|warning)\[(E\d{4})\] (?:([^\s:]*[^\s:\d][^\s:]*):)?(\d+)(?::(\d+))?$")]
    private static partial Regex EntryPattern();

    /// <summary>
    /// Compares the full ordered lists. Returns null when they are equal, otherwise both lists in
    /// the manifest encoding, expected above actual.
    /// </summary>
    public static string? Compare(IReadOnlyList<ExampleDiagnostic> expected, IReadOnlyList<ExampleDiagnostic> actual,
            string defaultFile) =>
        expected.SequenceEqual(actual)
            ? null
            : $"expected: {EncodeList(expected, defaultFile)}\nactual:   {EncodeList(actual, defaultFile)}";

    /// <summary>
    /// The manifest's <em>Observed</em> encoding: <c>severity[code] [file:]line[:column]</c>, entries
    /// joined by <c>"; "</c>, the file written only when it is not <paramref name="defaultFile"/>,
    /// and <c>none</c> for the empty list.
    /// </summary>
    public static string EncodeList(IReadOnlyList<ExampleDiagnostic> list, string defaultFile) =>
        list.Count == 0 ? None : string.Join(Separator, list.Select(d => d.Encode(defaultFile)));

    /// <summary>Reads <see cref="EncodeList"/>'s form back; on failure names the first bad entry.</summary>
    public static bool TryDecodeList(string encoded, string defaultFile,
            [NotNullWhen(true)] out IReadOnlyList<ExampleDiagnostic>? list, [NotNullWhen(false)] out string? badEntry) {
        list = null;
        badEntry = null;
        if (encoded == None) {
            list = [];
            return true;
        }

        var decoded = new List<ExampleDiagnostic>();
        foreach (string entry in encoded.Split(Separator)) {
            Match match = EntryPattern().Match(entry);
            if (!match.Success) {
                badEntry = entry;
                return false;
            }

            decoded.Add(new ExampleDiagnostic(
                match.Groups[1].Value,
                match.Groups[2].Value,
                match.Groups[3].Success ? match.Groups[3].Value : defaultFile,
                ParseInt(match.Groups[4].Value),
                match.Groups[5].Success ? ParseInt(match.Groups[5].Value) : null));
        }

        list = decoded;
        return true;
    }

    private string Encode(string defaultFile) {
        string file = File == defaultFile ? "" : File + ":";
        string column = Column is { } c ? ":" + c.ToString(CultureInfo.InvariantCulture) : "";
        return $"{Severity}[{Code}] {file}{Line.ToString(CultureInfo.InvariantCulture)}{column}";
    }

    internal static int ParseInt(string digits) => int.Parse(digits, CultureInfo.InvariantCulture);
}
