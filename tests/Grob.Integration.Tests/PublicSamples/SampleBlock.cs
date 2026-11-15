namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// One <c>grob</c> block in the public documentation (D-430 Decision 1). Its identity is its
/// repository-relative <paramref name="Path"/> and the 1-based <paramref name="Line"/> of its
/// opening fence; its display name adds the nearest heading, so a failure names the page and the
/// section.
/// </summary>
/// <param name="Path">The page, relative to the repository root, with forward slashes.</param>
/// <param name="Line">The 1-based line of the opening fence.</param>
/// <param name="Heading">The nearest heading above the fence, or null when there is none.</param>
/// <param name="Info">The info string after <c>grob</c>, trailing whitespace trimmed.</param>
/// <param name="Source">The block's body, one trailing newline.</param>
internal sealed record SampleBlock(string Path, int Line, string? Heading, string Info, string Source) {
    /// <summary><c>{path}:{line} ({heading})</c>, or <c>{path}:{line}</c> above the first heading.</summary>
    public string DisplayName => Heading is null ? $"{Path}:{Line}" : $"{Path}:{Line} ({Heading})";

    /// <inheritdoc/>
    public override string ToString() => DisplayName;
}
