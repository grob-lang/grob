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

    public long NextInt(long min, long max) {
        // NextInt64's upper bound is exclusive, so the inclusive contract normally
        // computes max + 1 — but that overflows when max is long.MaxValue, which
        // .NET's NextInt64 then rejects as "minValue > maxValue" (CodeRabbit review,
        // PR #130). Shifting the whole draw down by one and adding it back avoids the
        // overflow without computing max + 1; the one further edge (min also at
        // long.MinValue) draws directly from the generator's raw bit pattern instead,
        // since long.MinValue - 1 would itself overflow.
        if (min == long.MinValue && max == long.MaxValue) {
            Span<byte> buffer = stackalloc byte[8];
            _random.NextBytes(buffer);
            return BitConverter.ToInt64(buffer);
        }
        if (max == long.MaxValue) return _random.NextInt64(min - 1, max) + 1;
        return _random.NextInt64(min, max + 1);
    }

    public void Reseed(long seed) {
        // System.Random has no long-seed constructor; XOR-folding the upper and lower
        // 32 bits preserves more of the seed's entropy than a plain (int) truncation
        // would (CodeRabbit review, PR #130) — e.g. randomSeed(0) and
        // randomSeed(4294967296) no longer reseed identically.
        _random = new Random(unchecked((int)(seed ^ (seed >> 32))));
    }
}
