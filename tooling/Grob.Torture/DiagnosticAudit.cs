using System.Collections.Frozen;

using Grob.Core;

namespace Grob.Torture;

/// <summary>
/// Checks one diagnostic against the contract (gate report §1.1, corrections): its code is in
/// <see cref="ErrorCatalog.All"/>, and its location is inside the run's source. The start is
/// checked strictly, and the file too, since <see cref="SourceLocation.Unknown"/> passes a line
/// and column check. The end is checked only against end of file, because the parser ends many
/// nodes at the start of the next token (F-7).
/// </summary>
internal static class DiagnosticAudit {
    private static readonly FrozenSet<string> Registered =
        ErrorCatalog.All.Select(e => e.Code).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="code"/> is a registered error code.</summary>
    public static bool IsRegistered(string code) => Registered.Contains(code);

    /// <summary>The findings <paramref name="diagnostic"/> makes, at most one of each class.</summary>
    /// <param name="diagnostic">The diagnostic to check.</param>
    /// <param name="phase">The phase that reported it.</param>
    /// <param name="lineLengths">The run's source as <see cref="LineLengths"/> measures it, computed once per run.</param>
    /// <param name="fileName">The file name the run gave the lexer.</param>
    public static IReadOnlyList<Finding> Audit(Diagnostic diagnostic, PipelinePhase phase, int[] lineLengths, string fileName) {
        var findings = new List<Finding>(2);
        if (!IsRegistered(diagnostic.Code)) {
            findings.Add(new Finding(new Signature(phase, FindingClass.Unregistered, diagnostic.Code), diagnostic.ToString()));
        }
        if (FailedLocationCheck(diagnostic.Range, lineLengths, fileName) is { } check) {
            findings.Add(new Finding(
                new Signature(phase, FindingClass.Location, $"{diagnostic.Code}|{check}"), diagnostic.ToString()));
        }
        return findings;
    }

    private static string? FailedLocationCheck(SourceRange range, int[] lengths, string fileName) {
        SourceLocation start = range.Start;
        if (!string.Equals(start.File, fileName, StringComparison.Ordinal)) return "file";
        if (start.Line < 1 || start.Line > lengths.Length) return "line";
        if (start.Column < 1 || start.Column > lengths[start.Line - 1] + 1) return "column";
        return IsPastEndOfFile(range.End, lengths) ? "end" : null;
    }

    private static bool IsPastEndOfFile(SourceLocation end, int[] lengths) =>
        end.Line > lengths.Length || (end.Line == lengths.Length && end.Column > lengths[^1] + 1);

    /// <summary>
    /// The length of each line of <paramref name="source"/>, a CRLF ending's <c>'\r'</c> not
    /// counted. A source ending in a newline has an empty last line, so end of file is the start
    /// of that line.
    /// </summary>
    public static int[] LineLengths(string source) =>
        [.. source.Split('\n').Select(line => line.EndsWith('\r') ? line.Length - 1 : line.Length)];
}
