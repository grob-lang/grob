using System.Diagnostics.CodeAnalysis;

namespace Grob.DriftCheck;

/// <summary>
/// Console front-end for the consistency drift gate. Renders each check result
/// and maps the run to a process exit code. The verdict logic lives in
/// <see cref="ConsistencyChecks"/>; this type is the I/O sink.
/// </summary>
internal static class Cli {
    [ExcludeFromCodeCoverage(
        Justification = "I/O sink: writes to Console. The verdict logic is ConsistencyChecks (unit-tested).")]
    public static int Run() {
        try {
            var results = ConsistencyChecks.RunAll();
            foreach (var result in results) Console.WriteLine(result.Summarise());

            var failed = results.Count(r => !r.Ok);
            Console.WriteLine();
            Console.WriteLine(failed == 0
                ? $"Consistency drift gate: PASS ({results.Count} checks agreed)."
                : $"Consistency drift gate: FAIL ({failed} of {results.Count} checks found drift).");
            return failed == 0 ? 0 : 1;
        } catch (AnchorNotFoundException ex) {
            Console.Error.WriteLine($"DriftCheck: {ex.Message}");
            return 2;
        } catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) {
            Console.Error.WriteLine($"DriftCheck: {ex.Message}");
            return 2;
        }
    }
}
