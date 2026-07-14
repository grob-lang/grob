using Grob.Runtime;

namespace Grob.Cli;

/// <summary>
/// The composition root's <see cref="IClock"/> implementation (D-343 — the seam was
/// declared in Sprint 8 Increment A; <c>guid.newV7()</c>'s time-ordered generation is
/// Increment D's first real consumer). Wraps <see cref="DateTime.UtcNow"/> directly.
/// </summary>
internal sealed class SystemClock : IClock {
    public DateTime UtcNow => DateTime.UtcNow;
}
