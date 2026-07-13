using Grob.Runtime;

namespace Grob.Cli;

/// <summary>
/// The composition root's <see cref="IRandomSource"/> implementation (D-343 — the seam was
/// declared in Sprint 8 Increment A; <c>math.random*</c> is Increment B's first real
/// consumer). Wraps a mutable <see cref="Random"/> field, seeded from the clock by
/// construction so each VM run gets its own per-execution sequence unless the script calls
/// <c>math.randomSeed()</c>, which <see cref="Reseed"/> answers by replacing the field —
/// no state survives past this instance's VM run.
/// </summary>
internal sealed class SystemRandomSource : IRandomSource {
    private Random _random = new();

    public double NextDouble() => _random.NextDouble();

    public long NextInt(long min, long max) => _random.NextInt64(min, max + 1);

    public void Reseed(long seed) => _random = new Random((int)seed);
}
