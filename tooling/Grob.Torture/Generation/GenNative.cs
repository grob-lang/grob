namespace Grob.Torture.Generation;

/// <summary>A native the generator can call: a primitive's member, or a namespace function.</summary>
/// <param name="Owner">The receiver's type for a member; <see langword="null"/> for a namespace function.</param>
/// <param name="Name">The member's name, or the namespace function's qualified name, <c>math.sqrt</c>.</param>
/// <param name="Parameters">The required parameters' types, in order.</param>
/// <param name="Result">The result's type.</param>
/// <param name="IsProperty">Whether the member is read without a call, as <c>length</c> is.</param>
internal sealed record GenNative(GenType? Owner, string Name, IReadOnlyList<GenType> Parameters, GenType Result, bool IsProperty) {
    /// <inheritdoc/>
    public bool Equals(GenNative? other) =>
        other is not null && Owner == other.Owner && Name == other.Name && Result == other.Result
        && IsProperty == other.IsProperty && Parameters.SequenceEqual(other.Parameters);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Owner, Name, Result, IsProperty, Parameters.Count);
}
