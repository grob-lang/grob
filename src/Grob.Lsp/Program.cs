using OmniSharp.Extensions.LanguageServer.Server;
using System.Diagnostics.CodeAnalysis;

var server = await LanguageServer.From(options =>
    options
        .WithInput(Console.OpenStandardInput())
        .WithOutput(Console.OpenStandardOutput()));

await server.WaitForExit;

// Program.cs is the language server's host bootstrap: it starts the server over stdio
// and waits. Excluded from coverage per docs/dev/coverage-policy.md.
[ExcludeFromCodeCoverage(Justification = "Host bootstrap only: starts the OmniSharp language server over stdio and waits for exit. It has no branch; protocol and analysis logic belongs in Grob.Lsp proper, in scope for coverage.")]
internal partial class Program {
    protected Program() { }
}
