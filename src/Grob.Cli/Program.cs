using System.Diagnostics.CodeAnalysis;

Console.WriteLine("Grob");
return 0;


// Program.cs entry point — argv parsing and pipeline wiring.
// Exercised by end-to-end CLI tests, not unit tests.
// Excluded from coverage per project policy (see /docs/dev/coverage.md).
[ExcludeFromCodeCoverage(Justification = "CLI entry point; covered by integration tests. See docs/dev/coverage-policy.md.")]
internal partial class Program {
    protected Program() { }
}
