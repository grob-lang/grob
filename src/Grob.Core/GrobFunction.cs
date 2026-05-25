namespace Grob.Core;

/// <summary>
/// Runtime function/closure reference. Holds a compiled function body.
/// The full implementation lands with the compiler and closure support.
/// </summary>
public sealed class GrobFunction {
    /// <summary>The declared name of the function, or an empty string for anonymous lambdas.</summary>
    public string Name { get; }

    /// <summary>The number of parameters the function accepts.</summary>
    public int Arity { get; }

    /// <summary>
    /// Initialises a new <see cref="GrobFunction"/> with the given
    /// <paramref name="name"/> and <paramref name="arity"/>.
    /// </summary>
    public GrobFunction(string name, int arity) {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfNegative(arity);
        Name = name;
        Arity = arity;
    }
}
