namespace Grob.Core;

/// <summary>
/// An atomic unit of Grob source text produced by the lexer.
/// </summary>
public sealed record Token {
    /// <summary>Initialises a new <see cref="Token"/>.</summary>
    /// <param name="kind">The syntactic category of the token.</param>
    /// <param name="lexeme">
    /// The exact source text that produced the token.
    /// For structure tokens (<see cref="TokenKind.Newline"/>, <see cref="TokenKind.Eof"/>)
    /// pass an empty string.
    /// </param>
    /// <param name="location">The source position of the first character of the lexeme.</param>
    /// <param name="bracketDepth">
    /// The lexer's open-bracket nesting depth at the point the token was produced.
    /// Must be non-negative.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="lexeme"/> is <see langword="null"/>.</exception>
    /// <summary>
    /// Initializes a new <see cref="Token"/> with the specified kind, lexeme, source location, and bracket nesting depth.
    /// </summary>
    /// <param name="kind">Syntactic category of the token.</param>
    /// <param name="lexeme">Exact source text that produced the token (use the empty string for structural tokens such as Newline or Eof).</param>
    /// <param name="location">Source position of the first character of <paramref name="lexeme"/>.</param>
    /// <param name="bracketDepth">Lexer open-bracket nesting depth at token production time; must be greater than or equal to zero.</param>
    /// <exception cref="ArgumentNullException"><paramref name="lexeme"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bracketDepth"/> is negative.</exception>
    public Token(TokenKind kind, string lexeme, SourceLocation location, int bracketDepth) {
        if (bracketDepth < 0)
            throw new ArgumentOutOfRangeException(nameof(bracketDepth), "Bracket depth must be non-negative.");

        Kind = kind;
        Lexeme = lexeme ?? throw new ArgumentNullException(nameof(lexeme));
        Location = location;
        BracketDepth = bracketDepth;
    }

    /// <summary>The syntactic category of this token.</summary>
    public TokenKind Kind { get; }

    /// <summary>
    /// The exact source text that produced this token.
    /// For structure tokens (<see cref="TokenKind.Newline"/>, <see cref="TokenKind.Eof"/>)
    /// the lexeme is the empty string.
    /// </summary>
    public string Lexeme { get; }

    /// <summary>The source position of the first character of the lexeme.</summary>
    public SourceLocation Location { get; }

    /// <summary>
    /// The lexer's open-bracket nesting depth at the point this token was produced.
    /// The parser uses this to distinguish statement-boundary newlines (depth == 0)
    /// from newlines inside parentheses, square brackets or braces (depth &gt; 0),
    /// without needing to re-track bracket state during parsing.
    /// </summary>
    public int BracketDepth { get; }

    /// <summary>
    /// Returns a human-readable representation of this token, including its kind, lexeme,
    /// source location, and bracket depth.
    /// <summary>
            /// Produces a human-readable representation of the token including its kind, lexeme (or &quot;&lt;empty&gt;&quot; when empty), source location, and bracket depth.
            /// </summary>
            /// <returns>A string in the form: Token(&lt;Kind&gt;, &lt;lexeme or "&lt;empty&gt;"&gt;, &lt;Location&gt;, depth=&lt;BracketDepth&gt;).</returns>
    public override string ToString() =>
            $"Token({Kind}, {(Lexeme.Length == 0 ? "<empty>" : Lexeme)}, {Location}, depth={BracketDepth})";
}
