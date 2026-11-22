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

    /// <summary>
    /// The nested descriptor of parameter <paramref name="index"/>, or
    /// <see langword="null"/> when that parameter is not a function type, including when
    /// <see cref="ParameterDescriptors"/> is empty because no parameter is.
    /// </summary>
    public FunctionTypeDescriptor? ParameterDescriptorAt(int index) => DescriptorAt(ParameterDescriptors, index);

    /// <summary>
    /// Combines two function shapes that one value may take (D-448 Decision 7): the return
    /// paths of a lambda, or the arms of a ternary, a switch expression or <c>??</c>. An
    /// <see cref="GrobType.Unknown"/> parameter, as an inferred lambda carries, takes the
    /// other shape's type and nested shape, which only adds checks on the arguments. An
    /// Unknown return stays Unknown: the inferred lambda's body is not shown to produce the
    /// other shape's type, so no typed opcode may be chosen from it. Two concrete positions
    /// must be equal, and two function-typed positions must both carry a nested shape that
    /// combines in turn. Returns <see langword="null"/> when the arities differ or any
    /// position conflicts.
    /// </summary>
    public static FunctionTypeDescriptor? Merge(FunctionTypeDescriptor left, FunctionTypeDescriptor right) {
        int count = left.ParameterTypes.Count;
        if (count != right.ParameterTypes.Count) return null;

        var parameterTypes = new GrobType[count];
        var parameterDescriptors = new FunctionTypeDescriptor?[count];
        for (int i = 0; i < count; i++) {
            if (!TryMergePosition(
                    left.ParameterTypes[i], left.ParameterDescriptorAt(i),
                    right.ParameterTypes[i], right.ParameterDescriptorAt(i),
                    unknownTakesOther: true,
                    out parameterTypes[i], out parameterDescriptors[i])) {
                return null;
            }
        }
        return TryMergePosition(
                left.ReturnType, left.ReturnDescriptor, right.ReturnType, right.ReturnDescriptor,
                unknownTakesOther: false,
                out GrobType returnType, out FunctionTypeDescriptor? returnDescriptor)
            ? new FunctionTypeDescriptor(parameterTypes, returnType, parameterDescriptors, returnDescriptor)
            : null;
    }

    /// <summary>
    /// A position where at least one side is <see cref="GrobType.Unknown"/>: a parameter
    /// takes the other side's type and nested shape; a return stays Unknown.
    /// </summary>
    private static (GrobType Type, FunctionTypeDescriptor? Shape) MergeUnknownPosition(
            GrobType leftType, FunctionTypeDescriptor? leftShape,
            GrobType rightType, FunctionTypeDescriptor? rightShape,
            bool unknownTakesOther) {
        if (!unknownTakesOther) return (GrobType.Unknown, null);
        return leftType == GrobType.Unknown ? (rightType, rightShape) : (leftType, leftShape);
    }

    private static bool TryMergePosition(
            GrobType leftType, FunctionTypeDescriptor? leftShape,
            GrobType rightType, FunctionTypeDescriptor? rightShape,
            bool unknownTakesOther,
            out GrobType type, out FunctionTypeDescriptor? shape) {
        if (leftType == GrobType.Unknown || rightType == GrobType.Unknown) {
            (type, shape) = MergeUnknownPosition(leftType, leftShape, rightType, rightShape, unknownTakesOther);
            return true;
        }
        type = leftType;
        shape = null;
        if (leftType != rightType) return false;
        if (leftShape is null || rightShape is null) return leftShape is null && rightShape is null;
        shape = Merge(leftShape, rightShape);
        return shape is not null;
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
        // Strip trailing nulls so [] and [null, null, ...] hash identically, consistent
        // with ParameterDescriptorsEqual which treats missing entries as null.
        int lastIndex = ParameterDescriptors.Count - 1;
        while (lastIndex >= 0 && ParameterDescriptors[lastIndex] is null) {
            lastIndex--;
        }
        for (int i = 0; i <= lastIndex; i++) {
            hash.Add(ParameterDescriptors[i]);
        }
        return hash.ToHashCode();
    }
}
