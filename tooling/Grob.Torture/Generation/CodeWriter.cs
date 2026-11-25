using System.Text;

namespace Grob.Torture.Generation;

/// <summary>Writes a program line by line at four-space indentation, with line feeds only.</summary>
internal sealed class CodeWriter {
    private readonly StringBuilder _text = new();
    private int _indent;

    /// <summary>Writes one line at the current indentation.</summary>
    public void Line(string text) => _text.Append(' ', _indent * 4).Append(text).Append('\n');

    /// <summary>Writes <paramref name="header"/>, which ends with <c>{</c>, and indents what follows.</summary>
    public void Open(string header) {
        Line(header);
        _indent++;
    }

    /// <summary>Ends the block <see cref="Open"/> began, writing <paramref name="footer"/> at its indentation.</summary>
    public void Close(string footer = "}") {
        _indent--;
        Line(footer);
    }

    /// <summary>Ends one block and begins the next on the same line, as <c>} else {</c> does.</summary>
    public void Reopen(string text) {
        _indent--;
        Line(text);
        _indent++;
    }

    /// <inheritdoc/>
    public override string ToString() => _text.ToString();
}
