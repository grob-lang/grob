namespace Grob.Torture.Campaign;

/// <summary>How one run of the real CLI ended.</summary>
/// <param name="ExitCode">The process's exit code.</param>
/// <param name="Stderr">Everything it wrote to stderr.</param>
/// <param name="TimedOut">Whether the runner killed it at its timeout.</param>
internal sealed record GrobOutput(int ExitCode, string Stderr, bool TimedOut);
