using Grob.Runtime;

namespace Grob.Torture.Fakes;

/// <summary>A clock that always reads one instant, in UTC, so a run never reads the machine's time.</summary>
internal sealed class FixedClock : IClock {
    /// <summary>The instant every fake clock reads.</summary>
    public static readonly DateTime Instant = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <inheritdoc/>
    public DateTime UtcNow => Instant;

    /// <inheritdoc/>
    public TimeZoneInfo LocalZone => TimeZoneInfo.Utc;
}
