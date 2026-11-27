namespace Grob.Compiler;

/// <summary>
/// What a function type's parameter or return carries beyond its flat kind and nested
/// function shape (D-463): the declared name of a struct position, the element descriptor
/// of an array position and the value descriptor of a map position. The flat
/// <see cref="Grob.Core.GrobType"/> is the same for every struct, every array and every map,
/// so without it <c>fn(int[]): int</c> and <c>fn(float[]): int</c>, or <c>fn(A): int</c>
/// and <c>fn(B): int</c>, would be the same type. At most one channel is set, as the
/// position's kind decides; equal when every channel is.
/// </summary>
/// <param name="NamedTypeName">The declared name of a struct or named-type position.</param>
/// <param name="ArrayDescriptor">The element descriptor of an array position.</param>
/// <param name="MapDescriptor">The value descriptor of a map position.</param>
public sealed record PositionIdentity(
    string? NamedTypeName = null,
    ArrayTypeDescriptor? ArrayDescriptor = null,
    MapTypeDescriptor? MapDescriptor = null) {
    /// <summary>The identity with no channel set, for a position that carries none.</summary>
    public static PositionIdentity None { get; } = new();

    /// <summary>
    /// The identity of a position with the given channels, or <see langword="null"/> when
    /// none is set, so a scalar or function position carries none.
    /// </summary>
    public static PositionIdentity? Of(string? namedTypeName, ArrayTypeDescriptor? arrayDescriptor, MapTypeDescriptor? mapDescriptor) =>
        namedTypeName is null && arrayDescriptor is null && mapDescriptor is null
            ? null
            : new PositionIdentity(namedTypeName, arrayDescriptor, mapDescriptor);
}
