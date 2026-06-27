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

    // ---- Nullable variants — Sprint 3 Increment D (T? rules, D-014) ----

    /// <summary>Nullable 64-bit signed integer — the Grob <c>int?</c> type.</summary>
    NullableInt,

    /// <summary>Nullable 64-bit IEEE 754 double — the Grob <c>float?</c> type.</summary>
    NullableFloat,

    /// <summary>Nullable immutable UTF-8 string — the Grob <c>string?</c> type.</summary>
    NullableString,

    /// <summary>Nullable boolean — the Grob <c>bool?</c> type.</summary>
    NullableBool,

    // ---- Collection types — Sprint 4 Increment C (for...in iteration surface) ----

    /// <summary>
    /// Array — the Grob <c>array</c> type. Element type tracking awaits generics
    /// (Sprint 5); for now this is the unparameterised array tag the <c>for...in</c>
    /// lowering reads to select the array iteration shape.
    /// </summary>
    Array,

    /// <summary>
    /// Map — the Grob <c>map</c> type. Key and value type tracking awaits generics
    /// (Sprint 5); for now this is the unparameterised map tag the <c>for...in</c>
    /// lowering reads to select the map iteration shape.
    /// </summary>
    Map,

    // ---- Function types — Sprint 5 Increment 4 (D-326) ----

    /// <summary>
    /// Compile-time structural function type — <c>fn(T…): R</c>. Erased at runtime;
    /// the runtime value is always a <see cref="Grob.Core.GrobValue"/> wrapping a
    /// <c>GrobFunction</c>. The structural shape is carried in a
    /// <c>FunctionTypeDescriptor</c> alongside the symbol or expression node.
    /// </summary>
    Function,

    /// <summary>
    /// Nullable function type — <c>(fn(T…): R)?</c>. Nullable widening applies:
    /// a <see cref="Function"/> value is assignable to a <c>NullableFunction</c> slot
    /// with the same structural descriptor.
    /// </summary>
    NullableFunction,

    // ---- Nullable array — Sprint 5 Increment 5 (D-327) ----

    /// <summary>
    /// Nullable array — the Grob <c>T[]?</c> type. Nil is assignable;
    /// a non-nullable <c>array</c> value widens to this slot. Element-type
    /// tracking awaits generics (Sprint 5); the tag is the compile-time marker only.
    /// </summary>
    NullableArray,
}
