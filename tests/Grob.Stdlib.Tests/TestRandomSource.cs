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

    public long NextInt(long min, long max) => _random.NextInt64(min, max + 1);

    public void Reseed(long seed) => _random = new Random((int)seed);
}
