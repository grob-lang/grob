using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>Shared helpers for the lexer test suite.</summary>
internal static class LexerTestHelpers {
    /// <summary>Runs the lexer and asserts no diagnostics fired.</summary>
    public static IReadOnlyList<Token> Lex(string source) {
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        Assert.Empty(bag.Diagnostics);
        return tokens;
    }

    /// <summary>Runs the lexer and returns the tokens plus the diagnostics it produced.</summary>
    public static (IReadOnlyList<Token> Tokens, DiagnosticBag Diagnostics) LexWithDiagnostics(string source) {
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        return (tokens, bag);
    }

    /// <summary>Asserts the kind sequence (including the trailing <see cref="TokenKind.Eof"/>).</summary>
    public static void AssertKinds(IReadOnlyList<Token> tokens, params TokenKind[] expected) {
        TokenKind[] actual = [.. tokens.Select(t => t.Kind)];
        Assert.Equal(expected, actual);
    }

    /// <summary>Returns the single non-EOF token. Fails if the stream has any other content.</summary>
    public static Token SingleToken(string source) {
        IReadOnlyList<Token> tokens = Lex(source);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(TokenKind.Eof, tokens[^1].Kind);
        return tokens[0];
    }
}
