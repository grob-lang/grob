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

/// <summary>An <see cref="IClock"/> fixed at one instant.</summary>
internal sealed class FixedClock(DateTime utcNow) : IClock {
    public DateTime UtcNow { get; } = utcNow;
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
