using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// Compile-time descriptor for an array's element type (D-351). Mirrors
/// <see cref="FunctionTypeDescriptor"/>'s side-channel pattern: <see cref="GrobType.Array"/>
/// and <see cref="GrobType.NullableArray"/> stay flat tags in <c>Grob.Core</c>, and this
/// descriptor is carried alongside them — on a <see cref="Symbol"/>, an array-literal node,
/// or a call result — wherever an array-typed value's element identity is needed. Two
/// descriptors are equal when every channel is, so a function type's array position can be
/// compared by value (D-463).
/// </summary>
public sealed record ArrayTypeDescriptor {
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

    /// <summary>
    /// The element's function type when <see cref="ElementKind"/> is
    /// <see cref="GrobType.Function"/> or <see cref="GrobType.NullableFunction"/> (a
    /// <c>(fn(int): int)[]</c> element), so an element read out of the array, iterated or
    /// passed to a method carries its shape (D-463); <see langword="null"/> otherwise, or
    /// when the element's shape is not tracked.
    /// </summary>
    public FunctionTypeDescriptor? ElementFunctionDescriptor { get; }

    /// <summary>Initialises a descriptor for an array's element type.</summary>
    /// <param name="elementKind">The resolved flat kind of the array's element.</param>
    /// <param name="elementNamedTypeName">The element's declared user-type or <c>guid</c> name.</param>
    /// <param name="elementArrayDescriptor">The nested descriptor for a <c>T[][]</c> element.</param>
    /// <param name="elementFunctionDescriptor">The element's function type, for an array of functions.</param>
    public ArrayTypeDescriptor(
        GrobType elementKind,
        string? elementNamedTypeName = null,
        ArrayTypeDescriptor? elementArrayDescriptor = null,
        FunctionTypeDescriptor? elementFunctionDescriptor = null) {
        ElementKind = elementKind;
        ElementNamedTypeName = elementNamedTypeName;
        ElementArrayDescriptor = elementArrayDescriptor;
        ElementFunctionDescriptor = elementFunctionDescriptor;
    }
}
