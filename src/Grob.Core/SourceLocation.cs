namespace Grob.Core;

/// <summary>
/// A single point in source text, identified by file, line and column.
/// </summary>
public readonly record struct SourceLocation {
    /// <summary>
    /// Use for synthetic AST nodes, REPL fragments without files, and anywhere else
    /// where no real source position is available.
    /// </summary>
    public static readonly SourceLocation Unknown = new("<unknown>", 1, 1);

    /// <summary>The opaque file identifier.</summary>
    public string File { get; init; }

    /// <summary>The 1-based line number.</summary>
    public int Line { get; init; }

    /// <summary>The 1-based column number.</summary>
    public int Column { get; init; }

    /// <summary>
    /// Initialises a new <see cref="SourceLocation"/>.
    /// </summary>
    /// <param name="file">Opaque file identifier. Must not be null.</param>
    /// <param name="line">1-based line number. Must be >= 1.</param>
    /// <param name="column">1-based column number. Must be >= 1.</param>
    public SourceLocation(string file, int line, int column) {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        File = file;
        Line = line;
        Column = column;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{File}:{Line}:{Column}";
}
