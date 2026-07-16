using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// Compile-time descriptor for an array's element type (D-351). Mirrors
/// <see cref="FunctionTypeDescriptor"/>'s side-channel pattern: <see cref="GrobType.Array"/>
/// and <see cref="GrobType.NullableArray"/> stay flat tags in <c>Grob.Core</c>, and this
/// descriptor is carried alongside them — on a <see cref="Symbol"/>, an array-literal node,
/// or a call result — wherever an array-typed value's element identity is needed.
/// </summary>
public sealed class ArrayTypeDescriptor {
    /// <summary>The resolved flat kind of the array's element.</summary>
    public GrobType ElementKind { get; }

    /// <summary>
    /// The element's declared user-type or <c>guid</c> name when <see cref="ElementKind"/>
    /// is <see cref="GrobType.Struct"/> or <see cref="GrobType.NullableStruct"/>;
    /// <see langword="null"/> otherwise.
    /// </summary>
    public string? ElementNamedTypeName { get; }

    /// <summary>
    /// The nested element descriptor when <see cref="ElementKind"/> is
    /// <see cref="GrobType.Array"/> or <see cref="GrobType.NullableArray"/> (a <c>T[][]</c>
    /// element); <see langword="null"/> otherwise.
    /// </summary>
    public ArrayTypeDescriptor? ElementArrayDescriptor { get; }

    /// <summary>Initialises a descriptor for an array's element type.</summary>
    /// <param name="elementKind">The resolved flat kind of the array's element.</param>
    /// <param name="elementNamedTypeName">The element's declared user-type or <c>guid</c> name.</param>
    /// <param name="elementArrayDescriptor">The nested descriptor for a <c>T[][]</c> element.</param>
    public ArrayTypeDescriptor(
        GrobType elementKind,
        string? elementNamedTypeName = null,
        ArrayTypeDescriptor? elementArrayDescriptor = null) {
        ElementKind = elementKind;
        ElementNamedTypeName = elementNamedTypeName;
        ElementArrayDescriptor = elementArrayDescriptor;
    }
}
