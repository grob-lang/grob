namespace Grob.Runtime;

/// <summary>
/// The capability interface for pseudo-random number generation (Sprint 8 Increment A,
/// D-343, refining D-319). Declared here so the seam exists; <c>math.random()</c>,
/// <c>math.randomInt()</c> and <c>math.randomSeed()</c> (Increment B) and
/// <c>guid.newV4</c> (Increment D) are its first consumers. An OS-backed implementation
/// wraps <see cref="Random"/>; a test supplies a seeded or scripted sequence for
/// deterministic assertions.
/// </summary>
public interface IRandomSource {
    /// <summary>A uniform <see cref="double"/> in <c>[0.0, 1.0)</c>.</summary>
    double NextDouble();

    /// <summary>A uniform <see cref="long"/> in <c>[min, max]</c> — inclusive on both ends.</summary>
    long NextInt(long min, long max);

    /// <summary>Reseeds the generator so subsequent draws are reproducible.</summary>
    void Reseed(long seed);
}
