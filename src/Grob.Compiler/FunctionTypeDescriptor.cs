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
    /// The concrete return type a combined shape's branches are known to agree on, where
    /// <see cref="ReturnType"/> is <see cref="GrobType.Unknown"/> because an inferred
    /// lambda's return is; <see langword="null"/> when no branch returns a concrete type
    /// (D-448 Decision 7). A call's result reads <see cref="ReturnType"/> and stays
    /// Unknown; only an annotation check and a further combination read the bound.
    /// </summary>
    public GrobType? ReturnBound { get; }

    /// <summary>
    /// The nested descriptor of <see cref="ReturnBound"/> when it is a function type;
    /// <see langword="null"/> otherwise.
    /// </summary>
    public FunctionTypeDescriptor? ReturnBoundDescriptor { get; }

    /// <summary>
    /// True when <see cref="ReturnType"/> was read from a lambda's body rather than written
    /// in a type: the shape of a lambda literal, or of a value that only lambda literals
    /// gave it. A declared function type is compared invariantly (D-326, D-461), but the
    /// body of a lambda is a value the lambda produces, so as a value it is checked against
    /// the return it is given and may be an <c>int</c> where a <c>float</c> is declared, a
    /// <c>T</c> where <c>T?</c> is, or <c>nil</c> (<c>() =&gt; 1</c> for <c>fn(): float</c>,
    /// <c>() =&gt; nil</c> for <c>fn(): int?</c>). R-96 owns widening such a result. It is
    /// provenance, not part of the type, so it takes no part in <see cref="Equals(object?)"/>.
    /// </summary>
    public bool ReturnIsInferred { get; init; }

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

    /// <summary>
    /// A combined shape whose return is Unknown, carrying the concrete return its branches
    /// agree on as <see cref="ReturnBound"/>.
    /// </summary>
    private FunctionTypeDescriptor(
        IReadOnlyList<GrobType> parameterTypes,
        IReadOnlyList<FunctionTypeDescriptor?> parameterDescriptors,
        GrobType? returnBound,
        FunctionTypeDescriptor? returnBoundDescriptor)
        : this(parameterTypes, GrobType.Unknown, parameterDescriptors, null) {
        ReturnBound = returnBound;
        ReturnBoundDescriptor = returnBoundDescriptor;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is FunctionTypeDescriptor other &&
        ReturnType == other.ReturnType &&
        ReturnBound == other.ReturnBound &&
        ParameterTypes.SequenceEqual(other.ParameterTypes) &&
        NestedEqual(ReturnDescriptor, other.ReturnDescriptor) &&
        NestedEqual(ReturnBoundDescriptor, other.ReturnBoundDescriptor) &&
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
    /// other shape's type, so no typed opcode may be chosen from it. The concrete returns
    /// the shapes carry are combined into the result's <see cref="ReturnBound"/>, so an
    /// annotation or a further shape that conflicts with one is still refused. Two
    /// concrete positions must be equal, and two function-typed positions must both carry
    /// a nested shape that combines in turn. Returns <see langword="null"/> when the
    /// arities differ or any position, bound included, conflicts.
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
                    out parameterTypes[i], out parameterDescriptors[i])) {
                return null;
            }
        }
        bool inferred = ReturnStaysInferred(left, right);
        if (left.ReturnType != GrobType.Unknown && right.ReturnType != GrobType.Unknown) {
            return TryMergePosition(
                    left.ReturnType, left.ReturnDescriptor, right.ReturnType, right.ReturnDescriptor,
                    out GrobType returnType, out FunctionTypeDescriptor? returnDescriptor)
                ? new FunctionTypeDescriptor(parameterTypes, returnType, parameterDescriptors, returnDescriptor) { ReturnIsInferred = inferred }
                : null;
        }
        return TryMergeReturnBound(left, right, out GrobType? bound, out FunctionTypeDescriptor? boundShape)
            ? new FunctionTypeDescriptor(parameterTypes, parameterDescriptors, bound, boundShape) { ReturnIsInferred = inferred }
            : null;
    }

    /// <summary>
    /// The one rule for the provenance of a return when two shapes meet (D-461): the result
    /// is inferred only when every shape that gave it was, so one declared return makes it
    /// a declared type again, and nothing makes a declared return inferred. <see cref="Merge"/>
    /// applies it to the shapes it combines and <see cref="AfterAssigning"/> to a binding's
    /// shape and a value assigned to it.
    /// </summary>
    private static bool ReturnStaysInferred(FunctionTypeDescriptor left, FunctionTypeDescriptor right) =>
        left.ReturnIsInferred && right.ReturnIsInferred;

    /// <summary>
    /// The shape a binding holding this one has once <paramref name="assigned"/> is assigned
    /// to it (D-461): this shape where no <see cref="ReturnIsInferred"/> in it changes by
    /// <see cref="ReturnStaysInferred"/>, otherwise a copy where each has. The rule is
    /// applied at every level of the return, as <see cref="Merge"/> applies it: a function
    /// that returns a function is compared through the nested shape, so a nested return
    /// left inferred would let the conversion back in one level down. A copy, never a
    /// change in place, since a descriptor is shared by every binding and lambda it was
    /// read from: <c>j := k</c> then <c>k = declared</c> must leave <c>j</c> as it was. A
    /// level the assigned shape has no nested shape for is left as it is.
    /// </summary>
    public FunctionTypeDescriptor AfterAssigning(FunctionTypeDescriptor assigned) {
        bool inferred = ReturnStaysInferred(this, assigned);
        FunctionTypeDescriptor? held = KnownReturn(this).Shape;
        FunctionTypeDescriptor? updated = held is not null && KnownReturn(assigned).Shape is { } given
            ? held.AfterAssigning(given)
            : held;
        return inferred == ReturnIsInferred && ReferenceEquals(updated, held)
            ? this
            : new FunctionTypeDescriptor(this, updated) { ReturnIsInferred = inferred };
    }

    /// <summary>
    /// A copy of <paramref name="source"/> in every position, bound included, with
    /// <paramref name="returnShape"/> in place of the nested shape of its known return
    /// (the bound's where the return is Unknown). The provenance flag is left to the
    /// caller, as it is on every other construction.
    /// </summary>
    private FunctionTypeDescriptor(FunctionTypeDescriptor source, FunctionTypeDescriptor? returnShape) {
        bool boundForm = source.ReturnType == GrobType.Unknown;
        ParameterTypes = source.ParameterTypes;
        ReturnType = source.ReturnType;
        ParameterDescriptors = source.ParameterDescriptors;
        ReturnDescriptor = boundForm ? source.ReturnDescriptor : returnShape;
        ReturnBound = source.ReturnBound;
        ReturnBoundDescriptor = boundForm ? returnShape : source.ReturnBoundDescriptor;
    }

    /// <summary>
    /// The concrete return a shape is known to have: its return type when concrete, its
    /// bound when the return is Unknown, or <see langword="null"/> for neither.
    /// </summary>
    private static (GrobType? Type, FunctionTypeDescriptor? Shape) KnownReturn(FunctionTypeDescriptor descriptor) =>
        descriptor.ReturnType == GrobType.Unknown
            ? (descriptor.ReturnBound, descriptor.ReturnBoundDescriptor)
            : (descriptor.ReturnType, descriptor.ReturnDescriptor);

    /// <summary>
    /// Combines the concrete returns two shapes are known to have, where at least one
    /// return is Unknown. Either side knowing none takes the other's; two known returns
    /// must combine as two concrete positions do.
    /// </summary>
    private static bool TryMergeReturnBound(
            FunctionTypeDescriptor left, FunctionTypeDescriptor right,
            out GrobType? type, out FunctionTypeDescriptor? shape) {
        (GrobType? leftType, FunctionTypeDescriptor? leftShape) = KnownReturn(left);
        (GrobType? rightType, FunctionTypeDescriptor? rightShape) = KnownReturn(right);
        if (leftType is not { } knownLeft) {
            (type, shape) = (rightType, rightShape);
            return true;
        }
        if (rightType is not { } knownRight) {
            (type, shape) = (leftType, leftShape);
            return true;
        }
        bool combines = TryMergePosition(knownLeft, leftShape, knownRight, rightShape, out GrobType merged, out shape);
        type = merged;
        return combines;
    }

    /// <summary>
    /// Combines one parameter, or two concrete returns. An <see cref="GrobType.Unknown"/>
    /// side, as an inferred lambda's parameter is, takes the other side's type and nested
    /// shape; two concrete sides must be equal, nested shapes included.
    /// </summary>
    private static bool TryMergePosition(
            GrobType leftType, FunctionTypeDescriptor? leftShape,
            GrobType rightType, FunctionTypeDescriptor? rightShape,
            out GrobType type, out FunctionTypeDescriptor? shape) {
        if (leftType == GrobType.Unknown || rightType == GrobType.Unknown) {
            (type, shape) = leftType == GrobType.Unknown ? (rightType, rightShape) : (leftType, leftShape);
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
        hash.Add(ReturnBound);
        hash.Add(ReturnBoundDescriptor);
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
