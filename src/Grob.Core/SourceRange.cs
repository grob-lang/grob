namespace Grob.Core;

/// <summary>
/// A span between two <see cref="SourceLocation"/>s within the same file.
/// </summary>
public readonly record struct SourceRange {
    /// <summary>
    /// Use for synthetic AST nodes, REPL fragments without files, and anywhere else
    /// where no real source span is available.
    /// </summary>
    public static readonly SourceRange Unknown = new(SourceLocation.Unknown);

    /// <summary>The inclusive start of the range.</summary>
    public SourceLocation Start { get; init; }

    /// <summary>The inclusive end of the range.</summary>
    public SourceLocation End { get; init; }

    /// <summary>
    /// Initialises a new <see cref="SourceRange"/> spanning from <paramref name="start"/> to <paramref name="end"/>.
    /// </summary>
    /// <param name="start">The start location.</param>
    /// <param name="end">The end location. Must be in the same file as <paramref name="start"/> and at or after it.</param>
    public SourceRange(SourceLocation start, SourceLocation end) {
        if (start.File != end.File)
            throw new ArgumentException("A source range must lie within a single file.", nameof(end));

        if (end.Line < start.Line || (end.Line == start.Line && end.Column < start.Column))
            throw new ArgumentException("The end of a source range must be at or after its start.", nameof(end));

        Start = start;
        End = end;
    }

    /// <summary>
    /// Initialises a zero-width <see cref="SourceRange"/> at a single point.
    /// </summary>
    /// <param name="point">The point location. Both <see cref="Start"/> and <see cref="End"/> are set to this value.</param>
    public SourceRange(SourceLocation point) : this(point, point) {
    }

    /// <inheritdoc/>
    public override string ToString() =>
        Start == End
            ? $"{Start.File}:{Start.Line}:{Start.Column}"
            : $"{Start.File}:{Start.Line}:{Start.Column}-{End.Line}:{End.Column}";
}
