using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The closed union a <see cref="FunctionSignature"/> uses for a parameter or return
/// type (D-419 Decision 2, D-426 Decision 4): a <see cref="Concrete"/> Grob type, a
/// <see cref="TypeParameterReference"/> to one of the signature's own type parameters,
/// a <see cref="TypeParameterArray"/> of such a reference or a <see cref="FunctionShape"/>
/// for a function argument (D-443). The hierarchy is sealed (private base constructor)
/// so a consumer can switch over exactly four shapes, and each shape validates its own
/// fields so an invalid combination cannot exist.
/// </summary>
/// <remarks>
/// A signature is data (D-419 Decision 1): nothing here references an implementation,
/// and every member is something a manifest could serialise. <see cref="GrobType"/>
/// itself stays closed — the type-parameter shapes live here, not in the enum (D-419
/// Decision 3).
/// </remarks>
public abstract record SignatureType {
    private SignatureType() {
    }

    /// <summary>
    /// A concrete Grob type. <see cref="NamedTypeName"/> is permitted only when
    /// <see cref="Type"/> is <see cref="GrobType.Struct"/> or
    /// <see cref="GrobType.NullableStruct"/>, where it names the nominal type
    /// (<c>date</c>, <c>guid</c>) or is <see langword="null"/> to mean any struct.
    /// <see cref="ElementType"/> is permitted only when <see cref="Type"/> is
    /// <see cref="GrobType.Array"/> or <see cref="GrobType.NullableArray"/>, where it
    /// names the element type a checker threads out of the array
    /// (<c>string.split</c> returns <c>string[]</c>) or is <see langword="null"/> to
    /// mean an array of anything.
    /// </summary>
    public sealed record Concrete : SignatureType {
        /// <summary>
        /// Creates a concrete type. Throws <see cref="ArgumentException"/> when
        /// <paramref name="type"/> is unresolved (<see cref="GrobType.Unknown"/>,
        /// <see cref="GrobType.Error"/>), undefined or a function type — a function is
        /// described by <see cref="FunctionShape"/>, not by a bare tag (D-443) — or when
        /// <paramref name="namedTypeName"/> or <paramref name="elementType"/> is given
        /// for a tag that cannot carry it.
        /// </summary>
        public Concrete(GrobType type, string? namedTypeName = null, Concrete? elementType = null) {
            RequireResolved(type);
            if (namedTypeName is not null) {
                if (type is not (GrobType.Struct or GrobType.NullableStruct)) {
                    throw new ArgumentException(
                        $"A named type name is permitted only on Struct or NullableStruct, not {type}.",
                        nameof(namedTypeName));
                }
                Identifiers.RequireIdentifier(namedTypeName, nameof(namedTypeName), "A named type name");
            }
            if (elementType is not null) {
                if (type is not (GrobType.Array or GrobType.NullableArray)) {
                    throw new ArgumentException(
                        $"An element type is permitted only on Array or NullableArray, not {type}.",
                        nameof(elementType));
                }
                if (elementType.Type == GrobType.Nil) {
                    throw new ArgumentException("An array cannot have nil as its element type.", nameof(elementType));
                }
            }
            Type = type;
            NamedTypeName = namedTypeName;
            ElementType = elementType;
        }

        /// <summary>The Grob type tag.</summary>
        public GrobType Type { get; }

        /// <summary>The nominal struct name, or <see langword="null"/>. See the type remarks.</summary>
        public string? NamedTypeName { get; }

        /// <summary>The array element type, or <see langword="null"/>. See the type remarks.</summary>
        public Concrete? ElementType { get; }

        /// <summary><see langword="true"/> when <see cref="Type"/> is a nullable tag.</summary>
        public bool IsNullable => GrobTypeHelpers.IsNullable(Type);

        private static void RequireResolved(GrobType type) {
            if (type is GrobType.Unknown or GrobType.Error) {
                throw new ArgumentException($"A signature type must be a resolved Grob type, not {type}.", nameof(type));
            }
            if (type is GrobType.Function or GrobType.NullableFunction) {
                throw new ArgumentException(
                    "A concrete signature type cannot be a function type; describe a function argument with FunctionShape (D-443).",
                    nameof(type));
            }
            if (!Enum.IsDefined(type)) {
                throw new ArgumentException($"'{type}' is not a defined GrobType.", nameof(type));
            }
        }
    }

    /// <summary>
    /// A reference to one of the owning signature's type parameters, as in
    /// <c>mapAs&lt;T&gt;(): T</c>. The signature constructor checks that
    /// <see cref="Name"/> is declared.
    /// </summary>
    public sealed record TypeParameterReference : SignatureType {
        /// <summary>Creates a reference to the type parameter named <paramref name="name"/>.</summary>
        public TypeParameterReference(string name) {
            Identifiers.RequireIdentifier(name, nameof(name), "A type parameter name");
            Name = name;
        }

        /// <summary>The referenced type parameter's name.</summary>
        public string Name { get; }
    }

    /// <summary>
    /// An array whose element type is one of the owning signature's type parameters,
    /// as in <c>T[]</c>. The signature constructor checks that <see cref="Name"/> is
    /// declared.
    /// </summary>
    public sealed record TypeParameterArray : SignatureType {
        /// <summary>Creates an array of the type parameter named <paramref name="name"/>.</summary>
        public TypeParameterArray(string name) {
            Identifiers.RequireIdentifier(name, nameof(name), "A type parameter name");
            Name = name;
        }

        /// <summary>The element type parameter's name.</summary>
        public string Name { get; }
    }

    /// <summary>
    /// The type of a function a native takes as an argument, as in
    /// <c>replace(input: string, fn: (Match) =&gt; string)</c> (D-443, amending D-426
    /// Decision 1). Its parameters and return are <see cref="Concrete"/> only, so a shape
    /// never nests another function or a type parameter. <see cref="FunctionSignature"/>
    /// permits it as a parameter type only, and the catalogue builder only on a named
    /// type's method.
    /// </summary>
    public sealed record FunctionShape : SignatureType {
        private readonly Concrete[] _parameters;

        /// <summary>
        /// Creates a shape. Throws <see cref="ArgumentNullException"/> for a null list or
        /// return type and <see cref="ArgumentException"/> for a null or <c>nil</c>
        /// parameter type. A <c>nil</c> return means the function's result is unused.
        /// </summary>
        public FunctionShape(IReadOnlyList<Concrete> parameters, Concrete returnType) {
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(returnType);
            Concrete[] copied = [.. parameters];
            for (int i = 0; i < copied.Length; i++) {
                if (copied[i] is null) {
                    throw new ArgumentException($"Parameter {i} of a function shape is null.", nameof(parameters));
                }
                if (copied[i].Type == GrobType.Nil) {
                    throw new ArgumentException($"Parameter {i} of a function shape cannot have nil as its type.", nameof(parameters));
                }
            }
            _parameters = copied;
            ReturnType = returnType;
        }

        /// <summary>The function's parameter types, in call order.</summary>
        public IReadOnlyList<Concrete> Parameters => _parameters;

        /// <summary>The function's return type.</summary>
        public Concrete ReturnType { get; }

        /// <summary>Structural equality: the same parameter types in order and the same return.</summary>
        public bool Equals(FunctionShape? other) =>
            other is not null && ReturnType.Equals(other.ReturnType) && _parameters.SequenceEqual(other._parameters);

        /// <inheritdoc/>
        public override int GetHashCode() {
            var hash = new HashCode();
            hash.Add(ReturnType);
            foreach (Concrete parameter in _parameters) hash.Add(parameter);
            return hash.ToHashCode();
        }
    }

    /// <summary>A concrete type with no nominal name and no element type.</summary>
    public static Concrete Of(GrobType type) => new(type);

    /// <summary>A nominal struct type: <paramref name="type"/> must be Struct or NullableStruct.</summary>
    public static Concrete Named(GrobType type, string namedTypeName) => new(type, namedTypeName);

    /// <summary>A non-nullable array of <paramref name="elementType"/>.</summary>
    public static Concrete ArrayOf(Concrete elementType) {
        ArgumentNullException.ThrowIfNull(elementType);
        return new Concrete(GrobType.Array, elementType: elementType);
    }
}
