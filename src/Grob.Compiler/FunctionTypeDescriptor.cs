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
/// <para>
/// The flat kind cannot distinguish one array, struct or map from another either, so a
/// struct position's declared name, an array position's element descriptor and a map
/// position's value descriptor are carried in <see cref="ParameterIdentities"/> and
/// <see cref="ReturnIdentity"/> (D-463), which the checker compares invariantly.
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
    /// The identity of each parameter beyond its kind and nested shape (D-463),
    /// positionally aligned with <see cref="ParameterTypes"/>: the declared name of a struct
    /// parameter, the element descriptor of an array parameter or the value descriptor of a
    /// map parameter, <see langword="null"/> for any other. Empty when no parameter has one.
    /// </summary>
    public IReadOnlyList<PositionIdentity?> ParameterIdentities { get; init; } = [];

    /// <summary>
    /// The identity of the return beyond its kind and nested shape (D-463): the struct
    /// name, array element descriptor or map value descriptor it is declared with, or that
    /// a lambda's body gives it; <see langword="null"/> for any other return.
    /// </summary>
    public PositionIdentity? ReturnIdentity { get; init; }

    /// <summary>
    /// The identity of <see cref="ReturnBound"/>, as <see cref="ReturnIdentity"/> is of
    /// <see cref="ReturnType"/>; <see langword="null"/> when the bound has none.
    /// </summary>
    public PositionIdentity? ReturnBoundIdentity { get; }

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
    /// agree on as <see cref="ReturnBound"/>, or none.
    /// </summary>
    private FunctionTypeDescriptor(
        IReadOnlyList<GrobType> parameterTypes,
        IReadOnlyList<FunctionTypeDescriptor?> parameterDescriptors,
        Position? returnBound)
        : this(parameterTypes, GrobType.Unknown, parameterDescriptors, null) {
        ReturnBound = returnBound?.Type;
        ReturnBoundDescriptor = returnBound?.Shape;
        ReturnBoundIdentity = returnBound?.Identity;
    }

    /// <summary>
    /// One parameter or return of a shape: its kind, its nested function shape and its
    /// identity (D-463), combined as one when two shapes are.
    /// </summary>
    private readonly record struct Position(GrobType Type, FunctionTypeDescriptor? Shape, PositionIdentity? Identity);

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is FunctionTypeDescriptor other &&
        ReturnType == other.ReturnType &&
        ReturnBound == other.ReturnBound &&
        ParameterTypes.SequenceEqual(other.ParameterTypes) &&
        Equals(ReturnDescriptor, other.ReturnDescriptor) &&
        Equals(ReturnBoundDescriptor, other.ReturnBoundDescriptor) &&
        Equals(ReturnIdentity, other.ReturnIdentity) &&
        Equals(ReturnBoundIdentity, other.ReturnBoundIdentity) &&
        PositionallyEqual(ParameterDescriptors, other.ParameterDescriptors) &&
        PositionallyEqual(ParameterIdentities, other.ParameterIdentities);

    /// <summary>
    /// Compares two positional lists, treating an entry past a list's end as
    /// <see langword="null"/>, so the two-argument and four-argument constructors compare
    /// equal when no position is function-typed, and a shape with no identities equals one
    /// whose identities are all <see langword="null"/>.
    /// </summary>
    private static bool PositionallyEqual<T>(IReadOnlyList<T?> left, IReadOnlyList<T?> right) where T : class {
        int count = Math.Max(left.Count, right.Count);
        for (int i = 0; i < count; i++) {
            if (!Equals(EntryAt(left, i), EntryAt(right, i))) {
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
    public FunctionTypeDescriptor? ParameterDescriptorAt(int index) => EntryAt(ParameterDescriptors, index);

    /// <summary>
    /// The identity of parameter <paramref name="index"/> (D-463), or <see langword="null"/>
    /// when it has none, including when <see cref="ParameterIdentities"/> is empty because
    /// no parameter has one.
    /// </summary>
    public PositionIdentity? ParameterIdentityAt(int index) => EntryAt(ParameterIdentities, index);

    private Position ParameterAt(int index) =>
        new(ParameterTypes[index], ParameterDescriptorAt(index), ParameterIdentityAt(index));

    /// <summary>
    /// Combines two function shapes that one value may take (D-448 Decision 7): the return
    /// paths of a lambda, or the arms of a ternary, a switch expression or <c>??</c>. An
    /// <see cref="GrobType.Unknown"/> parameter, as an inferred lambda carries, takes the
    /// other shape's type and nested shape, which only adds checks on the arguments. An
    /// Unknown return stays Unknown: the inferred lambda's body is not shown to produce the
    /// other shape's type, so no typed opcode may be chosen from it. The concrete returns
    /// the shapes carry are combined into the result's <see cref="ReturnBound"/>, so an
    /// annotation or a further shape that conflicts with one is still refused. Two
    /// concrete positions must be equal, two function-typed positions must both carry
    /// a nested shape that combines in turn, and two identities must be equal, where a
    /// position with none takes the other's (D-463). Returns <see langword="null"/> when
    /// the arities differ or any position, bound included, conflicts.
    /// </summary>
    public static FunctionTypeDescriptor? Merge(FunctionTypeDescriptor left, FunctionTypeDescriptor right) {
        int count = left.ParameterTypes.Count;
        if (count != right.ParameterTypes.Count) return null;

        var parameterTypes = new GrobType[count];
        var parameterDescriptors = new FunctionTypeDescriptor?[count];
        var parameterIdentities = new PositionIdentity?[count];
        for (int i = 0; i < count; i++) {
            if (TryMergePosition(left.ParameterAt(i), right.ParameterAt(i)) is not { } merged) {
                return null;
            }
            (parameterTypes[i], parameterDescriptors[i], parameterIdentities[i]) = merged;
        }
        // The merged return is inferred only when every shape that gave it was: one
        // declared return makes the result a declared type again.
        bool inferred = left.ReturnIsInferred && right.ReturnIsInferred;
        if (left.ReturnType != GrobType.Unknown && right.ReturnType != GrobType.Unknown) {
            return TryMergePosition(DeclaredReturn(left), DeclaredReturn(right)) is { } returned
                ? new FunctionTypeDescriptor(parameterTypes, returned.Type, parameterDescriptors, returned.Shape) {
                    ReturnIsInferred = inferred,
                    ParameterIdentities = parameterIdentities,
                    ReturnIdentity = returned.Identity,
                }
                : null;
        }
        return TryMergeReturnBound(left, right, out Position? bound)
            ? new FunctionTypeDescriptor(parameterTypes, parameterDescriptors, bound) {
                ReturnIsInferred = inferred,
                ParameterIdentities = parameterIdentities,
            }
            : null;
    }

    private static Position DeclaredReturn(FunctionTypeDescriptor descriptor) =>
        new(descriptor.ReturnType, descriptor.ReturnDescriptor, descriptor.ReturnIdentity);

    /// <summary>
    /// The concrete return a shape is known to have: its return when concrete, its bound
    /// when the return is Unknown, or <see langword="null"/> for neither.
    /// </summary>
    private static Position? KnownReturn(FunctionTypeDescriptor descriptor) {
        if (descriptor.ReturnType != GrobType.Unknown) return DeclaredReturn(descriptor);
        return descriptor.ReturnBound is { } bound
            ? new Position(bound, descriptor.ReturnBoundDescriptor, descriptor.ReturnBoundIdentity)
            : null;
    }

    /// <summary>
    /// Combines the concrete returns two shapes are known to have, where at least one
    /// return is Unknown. Either side knowing none takes the other's; two known returns
    /// must combine as two concrete positions do.
    /// </summary>
    private static bool TryMergeReturnBound(FunctionTypeDescriptor left, FunctionTypeDescriptor right, out Position? bound) {
        Position? leftReturn = KnownReturn(left);
        Position? rightReturn = KnownReturn(right);
        if (leftReturn is not { } knownLeft) {
            bound = rightReturn;
            return true;
        }
        if (rightReturn is not { } knownRight) {
            bound = knownLeft;
            return true;
        }
        bound = TryMergePosition(knownLeft, knownRight);
        return bound is not null;
    }

    /// <summary>
    /// Combines one parameter, or two concrete returns. An <see cref="GrobType.Unknown"/>
    /// side, as an inferred lambda's parameter is, takes the other side's type, nested
    /// shape and identity; two concrete sides must be equal, nested shapes and identities
    /// included. <see langword="null"/> when they conflict.
    /// </summary>
    private static Position? TryMergePosition(Position left, Position right) {
        if (left.Type == GrobType.Unknown) return right;
        if (right.Type == GrobType.Unknown) return left;
        if (left.Type != right.Type) return null;
        if (left.Identity is not null && right.Identity is not null && !left.Identity.Equals(right.Identity)) return null;
        PositionIdentity? identity = left.Identity ?? right.Identity;
        if (left.Shape is null || right.Shape is null) {
            return left.Shape is null && right.Shape is null ? new Position(left.Type, null, identity) : null;
        }
        return Merge(left.Shape, right.Shape) is { } shape ? new Position(left.Type, shape, identity) : null;
    }

    private static T? EntryAt<T>(IReadOnlyList<T?> list, int index) where T : class =>
        index < list.Count ? list[index] : null;

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
        hash.Add(ReturnIdentity);
        hash.Add(ReturnBoundIdentity);
        AddPositional(ref hash, ParameterDescriptors);
        AddPositional(ref hash, ParameterIdentities);
        return hash.ToHashCode();
    }

    /// <summary>
    /// Adds a positional list to <paramref name="hash"/> without its trailing
    /// <see langword="null"/> entries, so <c>[]</c> and <c>[null, null]</c> hash
    /// identically, consistent with <see cref="PositionallyEqual{T}"/>.
    /// </summary>
    private static void AddPositional<T>(ref HashCode hash, IReadOnlyList<T?> list) where T : class {
        int lastIndex = list.Count - 1;
        while (lastIndex >= 0 && list[lastIndex] is null) {
            lastIndex--;
        }
        for (int i = 0; i <= lastIndex; i++) {
            hash.Add(list[i]);
        }
    }
}
