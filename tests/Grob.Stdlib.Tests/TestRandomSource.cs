using Grob.Runtime;

namespace Grob.Stdlib.Tests;

/// <summary>
/// A deterministic <see cref="IRandomSource"/> test double wrapping <see cref="Random"/> —
/// the same shape the production <c>SystemRandomSource</c> (<c>Grob.Cli</c>) uses, but
/// declared locally since the DAG forbids <c>Grob.Stdlib.Tests</c> from referencing
/// <c>Grob.Cli</c>. Unseeded construction still produces a reproducible sequence (fixed
/// seed), which is what the reproducibility tests need — production's per-execution,
/// clock-seeded default lives only in <c>Grob.Cli</c>.
/// </summary>
internal sealed class TestRandomSource : IRandomSource {
    private Random _random;

    internal TestRandomSource(int seed) => _random = new Random(seed);

    public double NextDouble() => _random.NextDouble();

    public long NextInt(long min, long max) {
        // Mirrors production SystemRandomSource's overflow fix (CodeRabbit review, PR
        // #130): max + 1 overflows when max is long.MaxValue.
        if (min == long.MinValue && max == long.MaxValue) {
            Span<byte> buffer = stackalloc byte[8];
            _random.NextBytes(buffer);
            return BitConverter.ToInt64(buffer);
        }
        if (max == long.MaxValue) return _random.NextInt64(min - 1, max) + 1;
        return _random.NextInt64(min, max + 1);
    }

    // XOR-folds the seed's upper/lower 32 bits, mirroring production's Reseed so the
    // two stay behaviourally identical (CodeRabbit review, PR #130).
    public void Reseed(long seed) => _random = new Random(unchecked((int)(seed ^ (seed >> 32))));
}
