namespace Grob.Torture;

/// <summary>How one run ended, and what it found.</summary>
/// <param name="Outcome">The outcome; <see cref="OutcomeKind.Finding"/> whenever <paramref name="Findings"/> is not empty.</param>
/// <param name="Findings">Every finding the run made, in the order the checks ran.</param>
/// <param name="ExitCode">The code a script passed to <c>exit(n)</c>, or <see langword="null"/>.</param>
internal sealed record OracleResult(OutcomeKind Outcome, IReadOnlyList<Finding> Findings, int? ExitCode = null) {
    /// <summary>
    /// Whether the run passed its watchdog. Its worker cannot be stopped, so a caller should run
    /// nothing further beside it.
    /// </summary>
    public bool TimedOut => Findings.Any(f => f.Signature.Class == FindingClass.Timeout);
}
