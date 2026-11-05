using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The closed union a <see cref="FunctionSignature"/> uses for a parameter or return
/// type (D-419 Decision 2, D-426 Decision 4): a <see cref="Concrete"/> Grob type, a
/// <see cref="TypeParameterReference"/> to one of the signature's own type parameters
/// or a <see cref="TypeParameterArray"/> of such a reference. The hierarchy is sealed
/// (private base constructor) so a consumer can switch over exactly three shapes, and
/// each shape validates its own fields so an invalid combination cannot exist.
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
        /// <see cref="GrobType.Error"/>), undefined or a function type — the union has
        /// no function shape by design (D-426 Decision 1) — or when
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
                    "A signature type cannot be a function type; the signature union has no function shape (D-426 Decision 1).",
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
