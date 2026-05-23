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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bracketDepth"/> is negative.</exception>
    public Token(TokenKind kind, string lexeme, SourceLocation location, int bracketDepth) {
        Kind = kind;
        Lexeme = lexeme ?? throw new ArgumentNullException(nameof(lexeme));
        Location = location;
        BracketDepth = bracketDepth >= 0
            ? bracketDepth
            : throw new ArgumentOutOfRangeException(nameof(bracketDepth), "Bracket depth must be non-negative.");
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
    /// </summary>
    public override string ToString() =>
            $"Token({Kind}, {(Lexeme.Length == 0 ? "<empty>" : Lexeme)}, {Location}, depth={BracketDepth})";
}
