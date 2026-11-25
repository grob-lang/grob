using Grob.Torture.Fakes;

namespace Grob.Torture.Generation;

/// <summary>
/// The generator's choices, drawn from SplitMix64 (gate report §4.5): a program depends on its
/// seed alone, and the sequence survives a .NET upgrade, which <see cref="Random"/>'s does not
/// promise.
/// </summary>
internal sealed class GenRandom(ulong seed) {
    private readonly SplitMixRandomSource _source = new(unchecked((long)seed));

    /// <summary>A number in <c>0..bound-1</c>.</summary>
    public int Next(int bound) => (int)_source.NextInt(0, bound - 1);

    /// <summary>A number in <c>min..max</c>, both inclusive.</summary>
    public int Between(int min, int max) => (int)_source.NextInt(min, max);

    /// <summary>True <paramref name="percent"/> times in a hundred.</summary>
    public bool Chance(int percent) => Next(100) < percent;

    /// <summary>One of <paramref name="items"/>, each equally likely.</summary>
    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];

    /// <summary>One of <paramref name="choices"/>, in proportion to its weight; the weights must not all be zero.</summary>
    public T Weighted<T>(IReadOnlyList<(int Weight, T Value)> choices) {
        int total = choices.Sum(c => c.Weight);
        if (total <= 0) throw new InvalidOperationException("the weights sum to zero");
        // A draw below the total falls within some weight, so the walk always returns.
        int draw = Next(total);
        int i = 0;
        while (draw >= choices[i].Weight) {
            draw -= choices[i].Weight;
            i++;
        }
        return choices[i].Value;
    }
}
