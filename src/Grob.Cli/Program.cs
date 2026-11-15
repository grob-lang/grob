using System.Diagnostics.CodeAnalysis;

using Grob.Cli;

return new CommandLineRunner(Console.In, Console.Out, Console.Error).Run(args);

// Program.cs binds the console streams to CommandLineRunner and nothing more. Every
// branch of the command line lives in CommandLineRunner, which is in scope for coverage
// and unit tested; see docs/dev/coverage-policy.md.
[ExcludeFromCodeCoverage(Justification = "Entry point only: binds Console.In, Console.Out and Console.Error to CommandLineRunner and returns its exit code. It has no branch; all argv parsing and dispatch is in CommandLineRunner, which is unit tested.")]
internal partial class Program {
    protected Program() { }
}
