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
    /// <remarks>
    /// Both bounds are inclusive, as the CLI's source has them. Draws below the threshold are
    /// rejected, so every value in the range is equally likely: a plain modulo would favour the
    /// low end of a range that does not divide 2^64.
    /// </remarks>
    public long NextInt(long min, long max) {
        ulong span = unchecked((ulong)(max - min) + 1UL);
        if (span == 0) return unchecked((long)NextBits());
        ulong threshold = unchecked(0UL - span) % span;
        ulong bits = NextBits();
        while (bits < threshold) {
            bits = NextBits();
        }
        return unchecked(min + (long)(bits % span));
    }

    /// <inheritdoc/>
    public void Reseed(long seed) => _state = unchecked((ulong)seed);
}
