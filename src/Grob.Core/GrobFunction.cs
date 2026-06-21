namespace Grob.Core;

/// <summary>
/// Runtime function reference — the common base for every callable Grob value.
/// Sprint 5 Increment A introduces the single concrete subclass
/// <see cref="BytecodeFunction"/> (a user <c>fn</c> with its own
/// <see cref="Chunk"/>). Later increments add lambda and native variants on the
/// same base, so a <see cref="GrobValueKind.Function"/> value is always a
/// <see cref="GrobFunction"/> regardless of which kind of callable it holds.
/// </summary>
public abstract class GrobFunction {
    /// <summary>The declared name of the function, or an empty string for anonymous lambdas.</summary>
    public string Name { get; }

    /// <summary>The number of parameters the function accepts.</summary>
    public int Arity { get; }

    /// <summary>
    /// Initialises a new <see cref="GrobFunction"/> with the given
    /// <paramref name="name"/> and <paramref name="arity"/>.
    /// </summary>
    protected GrobFunction(string name, int arity) {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfNegative(arity);
        Name = name;
        Arity = arity;
    }
}
