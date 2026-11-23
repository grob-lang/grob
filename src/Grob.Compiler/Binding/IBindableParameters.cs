namespace Grob.Compiler.Binding;

/// <summary>
/// The smallest view of a parameter list that named-argument binding needs: how many
/// parameters there are, each one's name and whether it has a default (D-113). A
/// script function's <see cref="Ast.Declarations.FnDecl"/> and a native's
/// <see cref="Runtime.FunctionSignature"/> both supply it, so one binder serves both
/// (D-446 Decision 1). Implemented by structs and taken as a generic constraint, so a
/// view costs no allocation.
/// </summary>
internal interface IBindableParameters {
    /// <summary>The number of parameters. A variadic tail counts as one.</summary>
    int Count { get; }

    /// <summary>The name of the parameter at <paramref name="index"/>.</summary>
    string NameAt(int index);

    /// <summary>
    /// Whether the parameter at <paramref name="index"/> has a default, and so may be
    /// left out or passed by name.
    /// </summary>
    bool HasDefaultAt(int index);
}
