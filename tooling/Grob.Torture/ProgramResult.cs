namespace Grob.Torture;

/// <summary>How one program in a slice ended.</summary>
/// <param name="ProgramId">The program's id: a seed's id for an unmutated seed.</param>
/// <param name="Outcome">The run's outcome.</param>
/// <param name="Findings">The run's findings.</param>
internal sealed record ProgramResult(string ProgramId, OutcomeKind Outcome, IReadOnlyList<Finding> Findings);
