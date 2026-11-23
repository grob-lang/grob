using Grob.Core;

namespace Grob.Torture.Mutation;

/// <summary>
/// Where each token sits in the source text, and how far an expression or a type extends from a
/// given token. The mutator edits source text at the nodes the parser returns, but a node's
/// <c>Range.End</c> is not reliable (register row R-74: the parser ends 41 node kinds at the start
/// of the next token, and a one-parameter lambda one token short). A node's <c>Range.Start</c> is
/// exact, so an operator takes a node's start from the tree and its extent from the tokens here
/// (gate report §1.6).
/// </summary>
internal sealed class TokenSpans {
    private readonly string _source;
    private readonly IReadOnlyList<Token> _tokens;
    private readonly int[] _lineStarts;
    private readonly int[] _starts;
    private readonly int[] _ends;

    /// <summary>Locates every token of <paramref name="tokens"/>, which <paramref name="source"/> produced.</summary>
    public TokenSpans(string source, IReadOnlyList<Token> tokens) {
        _source = source;
        _tokens = tokens;
        _lineStarts = LineStarts(source);
        _starts = new int[tokens.Count];
        _ends = new int[tokens.Count];
        for (int i = 0; i < tokens.Count; i++) {
            _starts[i] = OffsetOf(tokens[i].Location);
        }
        for (int i = 0; i < tokens.Count; i++) {
            _ends[i] = EndOf(i);
        }
    }

    /// <summary>The number of tokens, the end-of-file token included.</summary>
    public int Count => _tokens.Count;

    /// <summary>The token at <paramref name="index"/>.</summary>
    public Token this[int index] => _tokens[index];

    /// <summary>The offset of the token's first character.</summary>
    public int Start(int index) => _starts[index];

    /// <summary>The offset after the token's last character; equal to <see cref="Start"/> for a token with no text.</summary>
    public int End(int index) => _ends[index];

    /// <summary>The index of the token that starts at <paramref name="offset"/>, or -1.</summary>
    public int IndexAt(int offset) {
        int found = Array.BinarySearch(_starts, offset);
        if (found < 0) return -1;
        // Zero-width tokens can share an offset with the token after them; the one with text wins.
        while (found + 1 < _starts.Length && _starts[found + 1] == offset && _ends[found] == _starts[found]) found++;
        return found;
    }

    /// <summary>The index of the token that starts at <paramref name="location"/>, or -1.</summary>
    public int IndexAt(SourceLocation location) => IndexAt(OffsetOf(location));

    /// <summary>The offset of <paramref name="location"/> in the source, or -1 when it lies outside it.</summary>
    public int OffsetOf(SourceLocation location) {
        if (location.Line > _lineStarts.Length) return -1;
        int offset = _lineStarts[location.Line - 1] + location.Column - 1;
        return offset <= _source.Length ? offset : -1;
    }

    /// <summary>
    /// The index of the last token of the expression that starts at token <paramref name="first"/>:
    /// the scan runs until a newline, a comma or a closing bracket at the expression's own nesting,
    /// or the end of the file. Returns <paramref name="first"/> - 1 when the first token ends it.
    /// </summary>
    public int ExpressionLast(int first) => Scan(first, typeMode: false);

    /// <summary>
    /// The index of the last token of the type that starts at token <paramref name="first"/>. As
    /// <see cref="ExpressionLast"/>, and a type also ends at <c>:=</c>, <c>=</c>, <c>=&gt;</c> and an
    /// opening brace, and its <c>&lt; &gt;</c> nest.
    /// </summary>
    public int TypeLast(int first) => Scan(first, typeMode: true);

    /// <summary>The index of the token that closes the bracket opened at <paramref name="open"/>, or -1.</summary>
    public int MatchingClose(int open) {
        int nest = 0;
        for (int i = open; i < _tokens.Count; i++) {
            if (Opens(_tokens[i].Kind)) nest++;
            else if (Closes(_tokens[i].Kind) && --nest == 0) return i;
        }
        return -1;
    }

    /// <summary>The index of the first token at or after <paramref name="from"/> of <paramref name="kind"/>, or -1.</summary>
    public int Find(int from, TokenKind kind) {
        for (int i = from; i < _tokens.Count; i++) {
            if (_tokens[i].Kind == kind) return i;
        }
        return -1;
    }

    /// <summary>The text of <c>[start, end)</c>.</summary>
    public string Text(int start, int end) => _source[start..end];

    private int Scan(int first, bool typeMode) {
        int nest = 0;
        int last = first - 1;
        for (int i = first; i < _tokens.Count; i++) {
            TokenKind kind = _tokens[i].Kind;
            if (kind == TokenKind.Eof) break;
            if (nest == 0 && EndsAtTopLevel(kind, typeMode)) break;
            if (Opens(kind) || (typeMode && kind == TokenKind.Less)) {
                nest++;
            } else if (Closes(kind) || (typeMode && kind == TokenKind.Greater)) {
                if (nest == 0) break;
                nest--;
            }
            last = i;
        }
        return last;
    }

    private static bool EndsAtTopLevel(TokenKind kind, bool typeMode) => kind switch {
        TokenKind.Newline or TokenKind.Comma => true,
        TokenKind.ColonAssign or TokenKind.Assign or TokenKind.Arrow or TokenKind.LeftBrace => typeMode,
        _ => false,
    };

    private static bool Opens(TokenKind kind) =>
        kind is TokenKind.LeftParen or TokenKind.LeftBrace or TokenKind.LeftBracket or TokenKind.HashBrace or TokenKind.InterpStart;

    private static bool Closes(TokenKind kind) =>
        kind is TokenKind.RightParen or TokenKind.RightBrace or TokenKind.RightBracket or TokenKind.InterpEnd;

    // A lexeme is the raw text of its token for nearly every token, but not for a raw string
    // literal, whose lexeme leaves out its backticks, or a regex literal, whose lexeme leaves out
    // its slashes. So a lexeme is taken as the raw text only when the source says so at the
    // token's start. Otherwise the token runs to the start of the next one with its trailing
    // whitespace dropped.
    private int EndOf(int index) {
        string lexeme = _tokens[index].Lexeme;
        int start = _starts[index];
        if (start < 0) return start;
        if (lexeme.Length == 0) return start;
        if (string.CompareOrdinal(_source, start, lexeme, 0, lexeme.Length) == 0) return start + lexeme.Length;

        int next = index + 1;
        while (next < _tokens.Count && _starts[next] <= start) next++;
        int end = next < _tokens.Count ? _starts[next] : _source.Length;
        while (end > start + 1 && char.IsWhiteSpace(_source[end - 1])) end--;
        return end;
    }

    private static int[] LineStarts(string source) {
        var starts = new List<int> { 0 };
        for (int i = 0; i < source.Length; i++) {
            if (source[i] == '\n') starts.Add(i + 1);
        }
        return [.. starts];
    }

}
