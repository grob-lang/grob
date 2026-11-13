using System.Text.RegularExpressions;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>What <see cref="CliDiagnosticReader"/> read, and every line it could not account for.</summary>
internal sealed record CliDiagnostics(IReadOnlyList<ExampleDiagnostic> Diagnostics, IReadOnlyList<string> Problems);

/// <summary>
/// Reads the CLI's stderr into <see cref="ExampleDiagnostic"/>s, normalised as narrowly as D-428
/// Decision 2 allows. The rendered path must equal the path the harness passed, and the file is
/// then compared by bare name. A <c>line:col-line:col</c> range reduces to its start. A location
/// with no column keeps none. Message text is not read: that is R-25's to compare.
/// </summary>
internal static partial class CliDiagnosticReader {
    [GeneratedRegex(@"^(error|warning)\[(E\d{4})\]: ")]
    private static partial Regex HeaderPattern();

    // Lazy path: the passed path may hold a drive-letter colon, and the shortest prefix that
    // leaves a well-formed location is the path (gate report §1.3).
    [GeneratedRegex(@"^  --> (.+?):(\d+)(?::(\d+))?(?:-\d+:\d+)?$")]
    private static partial Regex LocationPattern();

    public static CliDiagnostics Read(string stderr, string passedPath) {
        var diagnostics = new List<ExampleDiagnostic>();
        var problems = new List<string>();
        string file = Path.GetFileName(passedPath);
        (string Severity, string Code)? pending = null;

        foreach (string line in ErrorExampleMaster.SplitLines(stderr)) {
            if (HeaderPattern().Match(line) is { Success: true } header) {
                ReportUnlocated(pending, problems);
                pending = (header.Groups[1].Value, header.Groups[2].Value);
            } else if (pending is { } open && LocationPattern().Match(line) is { Success: true } location) {
                pending = null;
                string rendered = location.Groups[1].Value;
                if (rendered != passedPath) {
                    problems.Add($"rendered path '{rendered}' is not the path passed '{passedPath}'");
                    continue;
                }

                int? column = location.Groups[3].Success ? ExampleDiagnostic.ParseInt(location.Groups[3].Value) : null;
                diagnostics.Add(new ExampleDiagnostic(open.Severity, open.Code, file,
                    ExampleDiagnostic.ParseInt(location.Groups[2].Value), column));
            } else {
                problems.Add($"unrecognised output line '{line}'");
            }
        }

        ReportUnlocated(pending, problems);
        return new CliDiagnostics(diagnostics, problems);
    }

    private static void ReportUnlocated((string Severity, string Code)? pending, List<string> problems) {
        if (pending is { } open) problems.Add($"diagnostic {open.Severity}[{open.Code}] has no location line");
    }
}
