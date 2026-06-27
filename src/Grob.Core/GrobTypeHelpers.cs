namespace Grob.Core;

/// <summary>
/// Static helpers for working with nullable <see cref="GrobType"/> variants
/// (Sprint 3 Increment D — T? type rules, D-014).
/// </summary>
public static class GrobTypeHelpers {
    /// <summary>
    /// Returns <c>true</c> when <paramref name="type"/> is one of the six
    /// nullable variants: <c>int?</c>, <c>float?</c>, <c>string?</c>,
    /// <c>bool?</c>, <c>fn?</c> (D-326) or <c>T[]?</c> (D-327).
    /// </summary>
    public static bool IsNullable(GrobType type) =>
        type is GrobType.NullableInt
             or GrobType.NullableFloat
             or GrobType.NullableString
             or GrobType.NullableBool
             or GrobType.NullableFunction
             or GrobType.NullableArray;

    /// <summary>
    /// Returns the nullable variant of <paramref name="type"/>.
    /// If <paramref name="type"/> is already nullable, or has no nullable
    /// counterpart (e.g. <c>nil</c>, <c>Unknown</c>), the input is returned
    /// unchanged.
    /// </summary>
    public static GrobType ToNullable(GrobType type) => type switch {
        GrobType.Int => GrobType.NullableInt,
        GrobType.Float => GrobType.NullableFloat,
        GrobType.String => GrobType.NullableString,
        GrobType.Bool => GrobType.NullableBool,
        GrobType.Function => GrobType.NullableFunction,
        GrobType.Array => GrobType.NullableArray,
        _ => type,
    };

    /// <summary>
    /// Returns the non-nullable element type of <paramref name="type"/>.
    /// For non-nullable types the input is returned unchanged.
    /// </summary>
    /// <example><c>ElementType(NullableInt) == Int</c></example>
    public static GrobType ElementType(GrobType type) => type switch {
        GrobType.NullableInt => GrobType.Int,
        GrobType.NullableFloat => GrobType.Float,
        GrobType.NullableString => GrobType.String,
        GrobType.NullableBool => GrobType.Bool,
        GrobType.NullableFunction => GrobType.Function,
        GrobType.NullableArray => GrobType.Array,
        _ => type,
    };
}
