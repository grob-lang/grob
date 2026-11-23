namespace Grob.Torture.Minimisation;

/// <summary>
/// What minimising a failing program gave. The original stays beside the reproducer, and nothing is
/// discarded because a reproducer matched another: keeping a signature does not keep the root
/// cause (prompt §1.4), so a reproducer that has slipped to another cause is only noticed by
/// comparing it with what else the program does.
/// </summary>
/// <param name="Original">The program as it failed.</param>
/// <param name="Reproducer">The smallest program found that still fails with the target signature, reducing from the front.</param>
/// <param name="Alternate">The smallest found reducing from the back, which differs from <paramref name="Reproducer"/> when two causes share the signature.</param>
/// <param name="DirectionsDisagree">Whether the two reproducers keep different constructs.</param>
/// <param name="SecondCauseSuspected">Whether the original still fails with the signature once the reproducer's lines are removed.</param>
/// <param name="Tests">How many candidates the minimiser ran.</param>
internal sealed record MinimisationResult(
    string Original,
    string Reproducer,
    string Alternate,
    bool DirectionsDisagree,
    bool SecondCauseSuspected,
    int Tests) {
    /// <summary>Whether a person should look for a second root cause before treating the reproducer as the whole story.</summary>
    public bool PossiblyMultiCause => DirectionsDisagree || SecondCauseSuspected;
}
