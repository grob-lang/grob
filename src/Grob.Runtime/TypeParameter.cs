namespace Grob.Runtime;

/// <summary>
/// A type parameter a native signature declares, as in <c>mapAs&lt;T&gt;(): T</c>
/// (D-418, D-419 Decision 2). Constraints are per signature, declared by the native
/// that owns the parameter. The shape is constructed and validated here; substitution
/// and constraint checking arrive with the <c>mapAs&lt;T&gt;</c> increment, and until
/// then a checker meeting a generic signature throws (D-426 Decision 4).
/// </summary>
public sealed record TypeParameter {
    private const TypeParameterConstraints AllConstraints =
        TypeParameterConstraints.NamedStruct | TypeParameterConstraints.ArrayOfNamedStruct;

    /// <summary>
    /// Creates a type parameter. Throws <see cref="ArgumentException"/> when
    /// <paramref name="name"/> is not an identifier or <paramref name="constraint"/>
    /// carries a bit <see cref="TypeParameterConstraints"/> does not define.
    /// </summary>
    public TypeParameter(string name, TypeParameterConstraints constraint = TypeParameterConstraints.None) {
        Identifiers.RequireIdentifier(name, nameof(name), "A type parameter name");
        if ((constraint & ~AllConstraints) != 0) {
            throw new ArgumentException($"'{constraint}' is not a defined TypeParameterConstraints.", nameof(constraint));
        }
        Name = name;
        Constraints = constraint;
    }

    /// <summary>The type parameter's name (<c>T</c>).</summary>
    public string Name { get; }

    /// <summary>The constraint on the type argument.</summary>
    public TypeParameterConstraints Constraints { get; }
}
