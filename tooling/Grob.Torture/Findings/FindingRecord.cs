namespace Grob.Torture.Findings;

/// <summary>
/// One failing program, kept whole: its id, where it came from, the signature it failed with, and
/// the source as it failed beside the reproducer that minimising found (prompt §1.4). A record is
/// keyed by its program, not by its signature, so one program's record never replaces another's.
/// </summary>
/// <param name="ProgramId">The program's id, <c>&lt;seed-id&gt;/&lt;operator&gt;/&lt;draw&gt;</c> for a mutant and the seed's id for a seed.</param>
/// <param name="Signature">The finding's signature key.</param>
/// <param name="Message">What was observed, unnormalised.</param>
/// <param name="FirstFrame">For an exception, the first Grob stack frame; a triage aid, not part of the signature.</param>
/// <param name="Original">The program as it failed.</param>
/// <param name="Reproducer">The smallest program found that fails the same way.</param>
/// <param name="PossiblyMultiCause">Whether minimising found a sign of a second root cause.</param>
internal sealed record FindingRecord(
    string ProgramId,
    string Signature,
    string Message,
    string? FirstFrame,
    string Original,
    string Reproducer,
    bool PossiblyMultiCause);
