using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// Compile-time structural descriptor for a function type: the parameter types
/// (positionally) and return type. Erased at runtime (D-326). Structural equality —
/// two descriptors are equal when they have the same arity, the same parameter types
/// in order, and the same return type.
/// </summary>
public sealed class FunctionTypeDescriptor {
    /// <summary>The resolved types of the function's parameters, in declaration order.</summary>
    public IReadOnlyList<GrobType> ParameterTypes { get; }

    /// <summary>The resolved return type of the function.</summary>
    public GrobType ReturnType { get; }

    /// <summary>Initialises a descriptor with the given parameter and return types.</summary>
    public FunctionTypeDescriptor(IReadOnlyList<GrobType> parameterTypes, GrobType returnType) {
        ParameterTypes = parameterTypes;
        ReturnType = returnType;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is FunctionTypeDescriptor other &&
        ReturnType == other.ReturnType &&
        ParameterTypes.SequenceEqual(other.ParameterTypes);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(ParameterTypes.Count, ReturnType);
}
