using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// Compile-time structural descriptor for a function type: the parameter types
/// (positionally) and return type. Erased at runtime (D-326). Structural equality —
/// two descriptors are equal when they have the same arity, the same parameter types
/// in order, the same return type, and the same nested descriptors for any
/// function-typed parameter or return position.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ParameterTypes"/> carries the flat <see cref="GrobType"/> kind for each
/// parameter; a function-typed parameter has kind <see cref="GrobType.Function"/> (or
/// <see cref="GrobType.NullableFunction"/>) there. The flat kind alone cannot
/// distinguish <c>fn(fn(): int): int</c> from <c>fn(fn(): string): int</c> — both have
/// a single <see cref="GrobType.Function"/> parameter — so the nested structural shape
/// is carried separately in <see cref="ParameterDescriptors"/> and
/// <see cref="ReturnDescriptor"/> (D-326 structural identity). A non-function position
/// has a <see langword="null"/> entry; a function position carries its descriptor.
/// </para>
/// </remarks>
public sealed class FunctionTypeDescriptor {
    /// <summary>The resolved types of the function's parameters, in declaration order.</summary>
    public IReadOnlyList<GrobType> ParameterTypes { get; }

    /// <summary>The resolved return type of the function.</summary>
    public GrobType ReturnType { get; }

    /// <summary>
    /// The nested structural descriptor for each parameter, positionally aligned with
    /// <see cref="ParameterTypes"/>. An entry is non-null only where the corresponding
    /// parameter is itself a function type; otherwise it is <see langword="null"/>.
    /// Empty when no parameter is function-typed.
    /// </summary>
    public IReadOnlyList<FunctionTypeDescriptor?> ParameterDescriptors { get; }

    /// <summary>
    /// The nested structural descriptor for the return type when it is itself a function
    /// type; <see langword="null"/> otherwise.
    /// </summary>
    public FunctionTypeDescriptor? ReturnDescriptor { get; }

    /// <summary>
    /// Initialises a descriptor with the given parameter and return types and no nested
    /// descriptors. Use this for function types whose parameters and return are all
    /// non-function types.
    /// </summary>
    public FunctionTypeDescriptor(IReadOnlyList<GrobType> parameterTypes, GrobType returnType)
        : this(parameterTypes, returnType, [], null) {
    }

    /// <summary>
    /// Initialises a descriptor with the given parameter and return types and the nested
    /// structural descriptors for any function-typed positions (D-326).
    /// </summary>
    /// <param name="parameterTypes">The resolved kinds of the parameters, in order.</param>
    /// <param name="returnType">The resolved kind of the return type.</param>
    /// <param name="parameterDescriptors">
    /// The nested descriptor for each parameter (null where the parameter is not a
    /// function type). Empty when no parameter is function-typed.
    /// </param>
    /// <param name="returnDescriptor">
    /// The nested descriptor for the return type, or <see langword="null"/> when the
    /// return type is not a function type.
    /// </param>
    public FunctionTypeDescriptor(
        IReadOnlyList<GrobType> parameterTypes,
        GrobType returnType,
        IReadOnlyList<FunctionTypeDescriptor?> parameterDescriptors,
        FunctionTypeDescriptor? returnDescriptor) {
        ParameterTypes = parameterTypes;
        ReturnType = returnType;
        ParameterDescriptors = parameterDescriptors;
        ReturnDescriptor = returnDescriptor;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is FunctionTypeDescriptor other &&
        ReturnType == other.ReturnType &&
        ParameterTypes.SequenceEqual(other.ParameterTypes) &&
        NestedEqual(ReturnDescriptor, other.ReturnDescriptor) &&
        ParameterDescriptorsEqual(other);

    private bool ParameterDescriptorsEqual(FunctionTypeDescriptor other) {
        // Treat an empty descriptor list as "all null" so the two-arg and four-arg
        // constructors compare equal when no position is function-typed.
        int count = Math.Max(ParameterDescriptors.Count, other.ParameterDescriptors.Count);
        for (int i = 0; i < count; i++) {
            if (!NestedEqual(DescriptorAt(ParameterDescriptors, i), DescriptorAt(other.ParameterDescriptors, i))) {
                return false;
            }
        }
        return true;
    }

    private static FunctionTypeDescriptor? DescriptorAt(IReadOnlyList<FunctionTypeDescriptor?> list, int index) =>
        index < list.Count ? list[index] : null;

    private static bool NestedEqual(FunctionTypeDescriptor? left, FunctionTypeDescriptor? right) =>
        left is null ? right is null : left.Equals(right);

    /// <inheritdoc/>
    public override int GetHashCode() {
        HashCode hash = new();
        hash.Add(ReturnType);
        foreach (GrobType parameterType in ParameterTypes) {
            hash.Add(parameterType);
        }
        hash.Add(ReturnDescriptor);
        foreach (FunctionTypeDescriptor? descriptor in ParameterDescriptors) {
            hash.Add(descriptor);
        }
        return hash.ToHashCode();
    }
}
