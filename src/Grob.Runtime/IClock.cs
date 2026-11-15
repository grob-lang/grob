namespace Grob.Runtime;

/// <summary>
/// The capability interface for wall-clock time (Sprint 8 Increment A, D-343, refining
/// D-319). The clock answers "what time is it here": the current instant and the local
/// zone it is read in (R-32), as .NET's own <c>TimeProvider</c> pairs <c>GetUtcNow</c> with
/// <c>LocalTimeZone</c>. The <c>date</c> module and <c>guid.newV7</c>'s time-ordered
/// generation are its consumers. An OS-backed implementation wraps
/// <see cref="DateTime.UtcNow"/> and <see cref="TimeZoneInfo.Local"/>; a test supplies a
/// fixed or stepped clock and a synthetic zone for deterministic assertions.
/// </summary>
public interface IClock {
    /// <summary>The current instant, UTC.</summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// The zone "local" means: <c>date.now</c>, <c>date.today</c>, <c>toLocal</c> and an
    /// offset-free <c>date.parse</c> resolve against it.
    /// </summary>
    TimeZoneInfo LocalZone { get; }
}
