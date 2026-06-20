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
