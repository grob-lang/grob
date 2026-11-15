using Grob.Core;

namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// D-430 Decision 2 to Decision 4's verdicts. Before either class is judged, every E1001 whose
/// subject is an excused namespace is set aside. A bare block left with no diagnostic but at least
/// one set aside passes as excused: the pipeline stopped at its compile errors, so it was not
/// executed and its exit code is not read. A bare block with nothing set aside must have run clean:
/// no diagnostic, no unaccounted stderr line (an internal error is one) and exit code 0. A fragment
/// must leave at least one diagnostic, every one E1001. A blocked block must fail its class.
/// </summary>
internal static class SampleVerdict {
    /// <summary>Null when the block meets its markers, otherwise why it does not.</summary>
    public static string? Check(SampleMarkers markers, SampleRun run) {
        string? failure = Judge(markers.Fragment, run);
        if (markers.BlockedBy.Count == 0) {
            return failure;
        }
        return failure is null
            ? $"passes its class but carries blocked-by={string.Join(", blocked-by=", markers.BlockedBy)}; remove the token"
            : null;
    }

    /// <summary>Null when the block passes its class, otherwise why it fails.</summary>
    public static string? Judge(bool fragment, SampleRun run) {
        List<SampleDiagnostic> remaining = [.. run.Diagnostics.Where(d => !IsExcused(d))];
        bool setAside = remaining.Count < run.Diagnostics.Count;
        return fragment ? JudgeFragment(remaining, run) : JudgeBare(remaining, setAside, run);
    }

    private static string? JudgeBare(List<SampleDiagnostic> remaining, bool setAside, SampleRun run) {
        if (remaining.Count > 0) {
            return $"writes {Codes(remaining)}";
        }
        if (setAside) {
            return null;
        }
        if (run.Problems.Count > 0) {
            return $"writes unaccounted output: {string.Join("; ", run.Problems)}";
        }
        return run.ExitCode == 0 ? null : $"exits {run.ExitCode} with no diagnostic";
    }

    private static string? JudgeFragment(List<SampleDiagnostic> remaining, SampleRun run) {
        if (remaining.Count == 0) {
            return "is marked fragment but writes no diagnostic once excused namespaces are set aside; " +
                "it is a complete program, so remove 'fragment'";
        }
        if (remaining.Any(d => d.Code != ErrorCatalog.E1001.Code)) {
            return $"is a fragment, which may write only {ErrorCatalog.E1001.Code}, but writes {Codes(remaining)}";
        }
        return run.Problems.Count > 0 ? $"writes unaccounted output: {string.Join("; ", run.Problems)}" : null;
    }

    private static bool IsExcused(SampleDiagnostic d) =>
        d.Code == ErrorCatalog.E1001.Code && d.Subject is not null && ExcusedNamespaces.All.Contains(d.Subject);

    private static string Codes(List<SampleDiagnostic> diagnostics) =>
        string.Join(", ", diagnostics.Select(d => d.Subject is null ? d.Code : $"{d.Code} ('{d.Subject}')"));
}
