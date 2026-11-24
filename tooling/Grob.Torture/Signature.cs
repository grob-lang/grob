namespace Grob.Torture;

/// <summary>
/// A finding's signature: the phase, the class and the fields that class defines (gate
/// report §4.7), joined into <see cref="Key"/>. Two programs with one root cause share a
/// signature; two root causes may share one too, so a signature never identifies a root
/// cause on its own (prompt §1.4).
/// </summary>
/// <remarks>
/// An exception's first stack frame is not a field. Tiered JIT compilation inlines methods
/// once they are hot, so the first frame of one failure can differ between an early and a
/// late program in one process, and between a Debug and a Release build. The frame is kept on
/// the <see cref="Finding"/> for triage instead.
/// </remarks>
/// <param name="Phase">The phase the finding arose in.</param>
/// <param name="Class">The finding's class.</param>
/// <param name="Detail">The class's own fields, already normalised.</param>
internal sealed record Signature(PipelinePhase Phase, FindingClass Class, string Detail) {
    /// <summary>The signature as one string, the form the known-findings list records.</summary>
    public string Key => $"{Phase}|{Class}|{Detail}";

    /// <inheritdoc/>
    public override string ToString() => Key;
}
