using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// Compile-time descriptor for a map's value type (Sprint 9 Increment C0b-1) — mirrors
/// <see cref="ArrayTypeDescriptor"/>'s side-channel pattern (D-351): <see cref="GrobType.Map"/>
/// stays a flat tag in <c>Grob.Core</c> and this descriptor is carried alongside it — on a
/// <see cref="Symbol"/>, or a call result — wherever a map-typed value's value identity is
/// needed. v1 keys are <c>string</c>-only (the type registry's <c>map&lt;K, V&gt;</c> section,
/// adjacent to D-080's constrained-generics model), so unlike <see cref="ArrayTypeDescriptor"/>'s
/// single element channel there is no key channel here — only <c>V</c>.
/// </summary>
public sealed class MapTypeDescriptor {
    /// <summary>The resolved flat kind of the map's value.</summary>
    public GrobType ValueKind { get; }

    /// <summary>
    /// The value's declared user-type or <c>guid</c> name when <see cref="ValueKind"/>
    /// is <see cref="GrobType.Struct"/> or <see cref="GrobType.NullableStruct"/>;
    /// <see langword="null"/> otherwise.
    /// </summary>
    public string? ValueNamedTypeName { get; }

    /// <summary>
    /// The element descriptor of the map's value when <see cref="ValueKind"/> is
    /// <see cref="GrobType.Array"/> or <see cref="GrobType.NullableArray"/> (a
    /// <c>map&lt;string, T[]&gt;</c> value); <see langword="null"/> otherwise.
    /// </summary>
    public ArrayTypeDescriptor? ValueArrayDescriptor { get; }

    /// <summary>Initialises a descriptor for a map's value type.</summary>
    /// <param name="valueKind">The resolved flat kind of the map's value.</param>
    /// <param name="valueNamedTypeName">The value's declared user-type or <c>guid</c> name.</param>
    /// <param name="valueArrayDescriptor">The element descriptor for a <c>map&lt;string, T[]&gt;</c> value.</param>
    public MapTypeDescriptor(
        GrobType valueKind,
        string? valueNamedTypeName = null,
        ArrayTypeDescriptor? valueArrayDescriptor = null) {
        ValueKind = valueKind;
        ValueNamedTypeName = valueNamedTypeName;
        ValueArrayDescriptor = valueArrayDescriptor;
    }
}
