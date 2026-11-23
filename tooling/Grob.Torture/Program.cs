using Grob.Torture;

// Grob.Torture — the crash oracle (D-451). Binds the console and dispatches; every command's
// logic is in Cli and the types it drives, which Grob.Torture.Tests covers.
return Cli.Run(args, Console.Out, Console.Error, Environment.CurrentDirectory);

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(
    Justification = "Console binding only: it passes the process's arguments and streams to Cli.Run, which the tests call directly with in-memory writers.")]
internal partial class Program { protected Program() { } }
