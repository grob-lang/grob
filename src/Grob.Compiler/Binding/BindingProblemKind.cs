namespace Grob.Compiler.Binding;

/// <summary>
/// The four ways a call can break the D-113 calling convention, one per diagnostic.
/// The binder reports the kind; the checker words the message for its callee.
/// </summary>
internal enum BindingProblemKind {
    /// <summary>A positional argument follows a named one (E0008).</summary>
    PositionalAfterNamed,

    /// <summary>A named argument names a parameter with no default (E0009).</summary>
    NamesRequired,

    /// <summary>A named argument names a parameter already supplied (E0010).</summary>
    Duplicate,

    /// <summary>A named argument names no parameter of the callee (E0011).</summary>
    Unknown,
}
