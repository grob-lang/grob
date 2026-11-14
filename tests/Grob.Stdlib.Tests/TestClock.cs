using Grob.Runtime;

namespace Grob.Stdlib.Tests;

/// <summary>
/// A settable <see cref="IClock"/> test double — the DAG forbids <c>Grob.Stdlib.Tests</c>
/// from referencing <c>Grob.Cli</c>, where the production <c>SystemClock</c> lives. The
/// local zone defaults to the host's, so a test that predates R-32 keeps its meaning; a
/// test that pins zone behaviour injects <see cref="TestZones.PlusThree"/>.
/// </summary>
internal sealed class TestClock : IClock {
    internal DateTime Now { get; set; }

    internal TestClock(DateTime now, TimeZoneInfo? localZone = null) {
        Now = now;
        LocalZone = localZone ?? TimeZoneInfo.Local;
    }

    public DateTime UtcNow => Now;

    public TimeZoneInfo LocalZone { get; }
}

/// <summary>
/// Synthetic zones for R-32's tests. Each has an offset no CI host is likely to run in,
/// so a native that ignores <see cref="IClock.LocalZone"/> and reads the host zone or
/// computes in UTC cannot pass by coincidence.
/// </summary>
internal static class TestZones {
    /// <summary>
    /// A +03:00 zone that springs forward to +04:00 at 02:00 on the last Sunday of March
    /// and falls back at 03:00 on the last Sunday of October.
    /// </summary>
    internal static TimeZoneInfo PlusThree { get; } = TimeZoneInfo.CreateCustomTimeZone(
        "Grob Test +03",
        TimeSpan.FromHours(3),
        "Grob Test +03",
        "Grob Test +03",
        "Grob Test +04",
        [
            TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                DateTime.MinValue.Date,
                DateTime.MaxValue.Date,
                TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 2, 0, 0, DateTimeKind.Unspecified), 3, 5, DayOfWeek.Sunday),
                TimeZoneInfo.TransitionTime.CreateFloatingDateRule(new DateTime(1, 1, 1, 3, 0, 0, DateTimeKind.Unspecified), 10, 5, DayOfWeek.Sunday)),
        ]);
}

/// <summary>An <see cref="IFileSystem"/> test double with a fixed current directory.</summary>
internal sealed class TestFileSystem(string currentDirectory) : IFileSystem {
    public string CurrentDirectory { get; } = currentDirectory;
}
