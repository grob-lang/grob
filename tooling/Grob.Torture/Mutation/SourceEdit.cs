using System.Text;

namespace Grob.Torture.Mutation;

/// <summary>
/// One edit to a source text: the characters in <c>[Start, End)</c> are replaced by
/// <paramref name="Text"/>. An insertion has <paramref name="Start"/> equal to <paramref name="End"/>,
/// and a deletion has empty text.
/// </summary>
/// <param name="Start">The offset of the first character replaced.</param>
/// <param name="End">The offset after the last character replaced.</param>
/// <param name="Text">What goes in its place.</param>
internal readonly record struct SourceEdit(int Start, int End, string Text) {
    /// <summary>Replaces <c>[start, end)</c> with <paramref name="text"/>.</summary>
    public static SourceEdit Replace(int start, int end, string text) => new(start, end, text);

    /// <summary>Inserts <paramref name="text"/> at <paramref name="offset"/>.</summary>
    public static SourceEdit Insert(int offset, string text) => new(offset, offset, text);

    /// <summary>Deletes <c>[start, end)</c>.</summary>
    public static SourceEdit Delete(int start, int end) => new(start, end, string.Empty);

    /// <summary>
    /// Applies <paramref name="edits"/> to <paramref name="source"/>. The edits are taken in offset
    /// order and must not overlap, so each is made against the original text.
    /// </summary>
    /// <exception cref="ArgumentException">An edit lies outside the source or overlaps another.</exception>
    public static string Apply(string source, IReadOnlyList<SourceEdit> edits) {
        var result = new StringBuilder(source.Length + 16);
        int cursor = 0;
        foreach (SourceEdit edit in edits.OrderBy(e => e.Start).ThenBy(e => e.End)) {
            if (edit.Start < cursor || edit.End < edit.Start || edit.End > source.Length) {
                throw new ArgumentException($"edit [{edit.Start}, {edit.End}) overlaps another or lies outside the source", nameof(edits));
            }
            result.Append(source, cursor, edit.Start - cursor).Append(edit.Text);
            cursor = edit.End;
        }
        return result.Append(source, cursor, source.Length - cursor).ToString();
    }
}
