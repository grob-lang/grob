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
    /// The erased parameter types, positionally, of this function's signature
    /// (D-336). Empty when the signature was not supplied — the runtime function
    /// type is erased (D-326), so this is display metadata carried alongside the
    /// value, not something the VM dispatches on. A display service renders it as
    /// <c>fn(int, string): bool</c>.
    /// </summary>
    public IReadOnlyList<GrobType> ParameterTypes { get; }

    /// <summary>
    /// The erased return type of this function's signature (D-336).
    /// <see cref="GrobType.Unknown"/> when the signature was not supplied.
    /// </summary>
    public GrobType ReturnType { get; }

    /// <summary>
    /// Initialises a new <see cref="GrobFunction"/> with the given
    /// <paramref name="name"/> and <paramref name="arity"/>, and an optional
    /// erased signature (<paramref name="parameterTypes"/> and
    /// <paramref name="returnType"/>) carried for display (D-336). When the
    /// signature is omitted, <see cref="ParameterTypes"/> is empty and
    /// <see cref="ReturnType"/> is <see cref="GrobType.Unknown"/>.
    /// </summary>
    protected GrobFunction(
        string name,
        int arity,
        IReadOnlyList<GrobType>? parameterTypes = null,
        GrobType returnType = GrobType.Unknown) {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfNegative(arity);
        // A supplied signature must have one type per parameter — a count that
        // disagrees with the arity is contradictory display metadata (D-336).
        if (parameterTypes is not null && parameterTypes.Count != arity)
            throw new ArgumentException(
                "Parameter type count must match the function arity.",
                nameof(parameterTypes));
        Name = name;
        Arity = arity;
        // Defensively copy so a caller holding the source list cannot mutate the
        // displayed signature after construction; expose a read-only wrapper.
        ParameterTypes = parameterTypes is null
            ? []
            : new List<GrobType>(parameterTypes).AsReadOnly();
        ReturnType = returnType;
    }
}
