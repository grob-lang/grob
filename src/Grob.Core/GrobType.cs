namespace Grob.Core;

/// <summary>
/// The Grob type system. Values are assigned by the type checker; Sprint 1 code
/// always sees <see cref="Unknown"/> — the concrete members land in Sprint 2.
/// </summary>
public enum GrobType {
    /// <summary>
    /// Not yet resolved. The default value for any <see cref="GrobType"/> field
    /// before the type checker (Sprint 2) runs.
    /// </summary>
    Unknown = 0,

    /// <summary>64-bit signed integer — the Grob <c>int</c> type (D-307).</summary>
    Int,

    /// <summary>64-bit IEEE 754 double — the Grob <c>float</c> type (D-307).</summary>
    Float,

    /// <summary>Immutable UTF-8 string — the Grob <c>string</c> type (D-307).</summary>
    String,

    /// <summary>Boolean — the Grob <c>bool</c> type (D-307).</summary>
    Bool,

    /// <summary>The absence-of-value type — the Grob <c>nil</c> type.</summary>
    Nil,

    /// <summary>
    /// Compiler-internal sentinel. Assigned to expressions whose type cannot be
    /// determined because an earlier sub-expression already produced an error.
    /// Universally assignable — a single mistake must not cascade into a storm of
    /// derived diagnostics. Never visible outside the type checker.
    /// </summary>
    Error,
}
