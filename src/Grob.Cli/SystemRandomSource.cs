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
        // PR #130). The common case is handled first so the two boundary arms below
        // both know max == long.MaxValue holds; the min == long.MinValue check is its
        // own plain branch (not folded into a compound condition) specifically so
        // min - 1 below is visibly guarded by "min != long.MinValue" to Sonar's flow
        // analysis (S3949) as well as at runtime — the two arms wrapped into one `if`
        // proved correct but not provably so to the analyzer.
        if (max != long.MaxValue) return _random.NextInt64(min, max + 1);
        if (min == long.MinValue) {
            // The one further edge: shifting down by one would itself underflow, so
            // draw directly from the generator's raw bit pattern instead — uniform
            // over the full signed 64-bit range, which is exactly what's needed here.
            Span<byte> buffer = stackalloc byte[8];
            _random.NextBytes(buffer);
            return BitConverter.ToInt64(buffer);
        }
        return _random.NextInt64(min - 1, max) + 1;
    }

    public void Reseed(long seed) {
        // System.Random has no long-seed constructor; XOR-folding the upper and lower
        // 32 bits preserves more of the seed's entropy than a plain (int) truncation
        // would (CodeRabbit review, PR #130) — e.g. randomSeed(0) and
        // randomSeed(4294967296) no longer reseed identically.
        _random = new Random(unchecked((int)(seed ^ (seed >> 32))));
    }
}
