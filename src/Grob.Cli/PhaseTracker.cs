namespace Grob.Cli;

/// <summary>
/// Records the phase one run (or one REPL entry) has reached, so the outermost handler can say
/// which phase an unexpected exception came from, and calls the command's phase hook as each
/// phase starts. The hook is a test seam (gate report §2.5): one that throws makes that phase
/// fail on demand, so no test depends on an open defect's reproducer.
/// </summary>
/// <param name="hook">Called as each phase starts; <see langword="null"/> in production.</param>
/// <param name="initial">The phase a run is in before the first <see cref="Enter"/>.</param>
internal sealed class PhaseTracker(Action<PipelinePhase>? hook, PipelinePhase initial = PipelinePhase.LoadingStandardLibrary) {
    /// <summary>The phase the run is in, or ended in.</summary>
    public PipelinePhase Current { get; private set; } = initial;

    /// <summary>Moves to <paramref name="phase"/>, then calls the hook.</summary>
    public void Enter(PipelinePhase phase) {
        Current = phase;
        hook?.Invoke(phase);
    }
}
