namespace Grob.Runtime;

/// <summary>
/// The capability interface for wall-clock time (Sprint 8 Increment A, D-343, refining
/// D-319). Declared here so the seam exists; the <c>date</c> module (Sprint 9) and
/// <c>guid.newV7</c>'s time-ordered generation (Increment D) are its first consumers. An
/// OS-backed implementation wraps <see cref="DateTime.UtcNow"/>; a test supplies a fixed
/// or stepped clock for deterministic assertions.
/// </summary>
public interface IClock {
    /// <summary>The current instant, UTC.</summary>
    DateTime UtcNow { get; }
}
