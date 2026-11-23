using Grob.Runtime;

namespace Grob.Torture.Fakes;

/// <summary>
/// A seeded random source over SplitMix64. Not <see cref="Random"/>: its seeded sequence is
/// not a documented contract across .NET versions, and a finding's seed must reproduce after
/// an upgrade (gate report §4.5).
/// </summary>
internal sealed class SplitMixRandomSource : IRandomSource {
    private ulong _state;

    /// <summary>Starts the sequence at <paramref name="seed"/>.</summary>
    public SplitMixRandomSource(long seed) => _state = unchecked((ulong)seed);

    /// <summary>The next 64 bits of the sequence.</summary>
    public ulong NextBits() {
        unchecked {
            ulong z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// <inheritdoc/>
    public double NextDouble() => (NextBits() >> 11) * (1.0 / (1UL << 53));

    /// <inheritdoc/>
    /// <remarks>Both bounds are inclusive, as the CLI's source has them.</remarks>
    public long NextInt(long min, long max) {
        ulong span = unchecked((ulong)(max - min) + 1UL);
        return span == 0 ? unchecked((long)NextBits()) : unchecked(min + (long)(NextBits() % span));
    }

    /// <inheritdoc/>
    public void Reseed(long seed) => _state = unchecked((ulong)seed);
}
