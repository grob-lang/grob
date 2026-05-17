using OmniSharp.Extensions.LanguageServer.Server;
using System.Diagnostics.CodeAnalysis;

var server = await LanguageServer.From(options =>
    options
        .WithInput(Console.OpenStandardInput())
        .WithOutput(Console.OpenStandardOutput()));

await server.WaitForExit;

// Program.cs entry point — argv parsing and pipeline wiring.
// Exercised by end-to-end CLI tests, not unit tests.
// Excluded from coverage per project policy (see /docs/dev/coverage.md).
[ExcludeFromCodeCoverage(Justification = "CLI entry point; covered by integration tests. See docs/dev/coverage-policy.md.")]
internal partial class Program {
    protected Program() { }
}
