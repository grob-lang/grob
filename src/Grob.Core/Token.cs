namespace Grob.Core;

/// <summary>
/// An atomic unit of Grob source text produced by the lexer.
/// </summary>
/// <param name="Kind">The syntactic category of this token.</param>
/// <param name="Lexeme">
/// The exact source text that produced this token.
/// For structure tokens (<see cref="TokenKind.Newline"/>, <see cref="TokenKind.Eof"/>)
/// the lexeme is the empty string.
/// </param>
/// <param name="Location">The source position of the first character of the lexeme.</param>
/// <param name="BracketDepth">
/// The lexer's open-bracket nesting depth at the point this token was produced.
/// The parser uses this to distinguish statement-boundary newlines (depth == 0)
/// from newlines inside parentheses, square brackets or braces (depth &gt; 0),
/// without needing to re-track bracket state during parsing.
/// </param>
public sealed record Token(
    TokenKind Kind,
    string Lexeme,
    SourceLocation Location,
    int BracketDepth) {
    /// <inheritdoc/>
    public override string ToString() =>
        $"Token({Kind}, {(Lexeme.Length == 0 ? "<empty>" : Lexeme)}, {Location}, depth={BracketDepth})";
}
