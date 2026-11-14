using Grob.Runtime;

namespace Grob.Integration.Tests;

/// <summary>
/// A dictionary-backed <see cref="IEnvironment"/> for tests that drive <c>RunCommand</c>
/// through its internal capabilities seam (D-428 Decision 3). Holds only the variables the
/// test supplies, so a script never sees the host process's environment.
/// </summary>
internal sealed class MapEnvironment : IEnvironment {
    private readonly Dictionary<string, string> _values;

    public MapEnvironment(IReadOnlyDictionary<string, string>? values = null) {
        _values = values is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(values, StringComparer.Ordinal);
    }

    public string? Get(string key) => _values.GetValueOrDefault(key);

    public void Set(string key, string value) => _values[key] = value;

    public bool Has(string key) => _values.ContainsKey(key);

    public IReadOnlyDictionary<string, string> All() => _values;
}

/// <summary>
/// An <see cref="IClock"/> fixed at one instant, in <see cref="HarnessZone"/> unless the test
/// supplies another zone.
/// </summary>
internal sealed class FixedClock(DateTime utcNow, TimeZoneInfo? localZone = null) : IClock {
    /// <summary>
    /// The harness's local zone (R-32): a custom zone at +03:00 with no daylight saving. Its
    /// offset is non-zero, so a native that ignores <see cref="IClock.LocalZone"/> and computes
    /// in UTC cannot pass by coincidence, and no CI host runs in it.
    /// </summary>
    internal static TimeZoneInfo HarnessZone { get; } =
        TimeZoneInfo.CreateCustomTimeZone("Grob Harness +03", TimeSpan.FromHours(3), "Grob Harness +03", "Grob Harness +03");

    public DateTime UtcNow { get; } = utcNow;

    public TimeZoneInfo LocalZone { get; } = localZone ?? HarnessZone;
}

/// <summary>
/// An <see cref="IFileSystem"/> whose current directory is fixed (R-32), so
/// <c>path.resolve</c> never reads the test process's working directory.
/// </summary>
internal sealed class FixedFileSystem(string currentDirectory) : IFileSystem {
    /// <summary>
    /// The harness's current directory: rooted on either CI leg and never created, because
    /// <c>path.resolve</c> only joins strings against it.
    /// </summary>
    internal static string HarnessDirectory { get; } = Path.Join(Path.GetTempPath(), "grob-harness");

    public string CurrentDirectory { get; } = currentDirectory;
}

/// <summary>
/// An <see cref="IRandomSource"/> that always draws the same value: <c>0.5</c> for
/// <see cref="NextDouble"/> and <c>min</c> for <see cref="NextInt"/>. Reseeding changes
/// nothing, so every run is reproducible.
/// </summary>
internal sealed class FixedRandomSource : IRandomSource {
    public double NextDouble() => 0.5;

    public long NextInt(long min, long max) => min;

    public void Reseed(long seed) {
        // Deliberately empty: the sequence is constant, so there is nothing to reseed.
    }
}
