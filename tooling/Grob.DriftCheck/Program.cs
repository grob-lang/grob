using Grob.DriftCheck;

// Grob.DriftCheck — the corpus consistency drift gate (D-316).
//
// Runs the same checks the xUnit gate (Grob.Consistency.Tests) runs, over the
// shared ConsistencyChecks library, for local `dotnet run` convenience. The
// xUnit suite is the gate that fails the build; this is the console mirror.
//
//   exit 0  every check agreed
//   exit 1  at least one check found drift
//   exit 2  a check could not locate its anchor / IO error

return Cli.Run();

// CLI entry point. The check logic lives in ConsistencyChecks (unit-tested by
// Grob.Consistency.Tests); this file is the I/O sink and is covered by that gate.
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(
    Justification = "CLI entry point; the checks are unit-tested in Grob.Consistency.Tests and the wiring is exercised by `dotnet run`.")]
internal partial class Program { protected Program() { } }

internal static class Cli {
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(
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
