namespace Grob.Torture.Generation;

/// <summary>
/// A type the generator can ask an expression for (prompt §2 group 5): the v1 surface's
/// scalars, arrays, string-keyed maps, nullables, function types, the struct types a program
/// declares and anonymous structs. Each writes the annotation a program spells it with, except an
/// anonymous struct, which a program cannot name and so binds only by inference.
/// </summary>
internal abstract record GenType {
    /// <summary><c>int</c>.</summary>
    public static GenType Int { get; } = new Scalar("int");

    /// <summary><c>float</c>.</summary>
    public static GenType Float { get; } = new Scalar("float");

    /// <summary><c>string</c>.</summary>
    public static GenType String { get; } = new Scalar("string");

    /// <summary><c>bool</c>.</summary>
    public static GenType Bool { get; } = new Scalar("bool");

    /// <summary>The annotation a program writes for this type.</summary>
    /// <exception cref="InvalidOperationException">The type holds an anonymous struct, which has no spelling.</exception>
    public abstract string Annotation { get; }

    /// <summary>Whether <see cref="Annotation"/> can be written: false when an anonymous struct is anywhere in the type.</summary>
    public virtual bool CanAnnotate => true;

    /// <summary>One of the four scalar types, by its keyword.</summary>
    internal sealed record Scalar(string Keyword) : GenType {
        /// <inheritdoc/>
        public override string Annotation => Keyword;
    }

    /// <summary><c>T[]</c>.</summary>
    internal sealed record ArrayOf(GenType Element) : GenType {
        /// <inheritdoc/>
        public override string Annotation => $"{Element.Annotation}[]";

        /// <inheritdoc/>
        public override bool CanAnnotate => Element.CanAnnotate;
    }

    /// <summary><c>map&lt;string, T&gt;</c>: the generator's maps are keyed by string.</summary>
    internal sealed record MapOf(GenType Value) : GenType {
        /// <inheritdoc/>
        public override string Annotation => $"map<string, {Value.Annotation}>";

        /// <inheritdoc/>
        public override bool CanAnnotate => Value.CanAnnotate;
    }

    /// <summary><c>T?</c>.</summary>
    internal sealed record Nullable(GenType Inner) : GenType {
        /// <inheritdoc/>
        public override string Annotation => $"{Inner.Annotation}?";

        /// <inheritdoc/>
        public override bool CanAnnotate => Inner.CanAnnotate;
    }

    /// <summary><c>fn(T, …): U</c>, compared by its parameter and return types.</summary>
    internal sealed record Function(IReadOnlyList<GenType> Parameters, GenType Return) : GenType {
        /// <inheritdoc/>
        public override string Annotation => $"fn({string.Join(", ", Parameters.Select(p => p.Annotation))}): {Return.Annotation}";

        /// <inheritdoc/>
        public override bool CanAnnotate => Return.CanAnnotate && Parameters.All(p => p.CanAnnotate);

        /// <inheritdoc/>
        public bool Equals(Function? other) =>
            other is not null && Return == other.Return && Parameters.SequenceEqual(other.Parameters);

        /// <inheritdoc/>
        public override int GetHashCode() => Parameters.Aggregate(Return.GetHashCode(), HashCode.Combine);
    }

    /// <summary>A struct type the program declares, compared by name: Grob's struct types are nominal.</summary>
    internal sealed record Struct(string Name, IReadOnlyList<GenField> Fields) : GenType {
        /// <inheritdoc/>
        public override string Annotation => Name;

        /// <inheritdoc/>
        public bool Equals(Struct? other) => other is not null && Name == other.Name;

        /// <inheritdoc/>
        public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
    }

    /// <summary>An anonymous struct, <c>#{ … }</c>, compared by its fields in order.</summary>
    internal sealed record Anonymous(IReadOnlyList<GenField> Fields) : GenType {
        /// <inheritdoc/>
        public override string Annotation => throw new InvalidOperationException("an anonymous struct has no annotation");

        /// <inheritdoc/>
        public override bool CanAnnotate => false;

        /// <inheritdoc/>
        public bool Equals(Anonymous? other) => other is not null && Fields.SequenceEqual(other.Fields);

        /// <inheritdoc/>
        public override int GetHashCode() => Fields.Aggregate(0, HashCode.Combine);
    }
}
