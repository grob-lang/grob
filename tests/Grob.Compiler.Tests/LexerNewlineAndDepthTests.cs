using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerNewlineAndDepthTests {
    [Fact]
    public void Newline_IsEmittedAtTopLevel() {
        AssertKinds(Lex("a\nb"),
            TokenKind.Identifier, TokenKind.Newline, TokenKind.Identifier, TokenKind.Eof);
    }

    [Fact]
    public void TrailingOperator_SuppressesNewline() {
        // "a +\nb" — the trailing + means the line continues.
        AssertKinds(Lex("a +\nb"),
            TokenKind.Identifier, TokenKind.Plus, TokenKind.Identifier, TokenKind.Eof);
    }

    [Fact]
    public void TrailingComma_SuppressesNewline() {
        AssertKinds(Lex("f(\na,\nb\n)"),
            TokenKind.Identifier, TokenKind.LeftParen,
            TokenKind.Identifier, TokenKind.Comma,
            TokenKind.Identifier,
            TokenKind.RightParen,
            TokenKind.Eof);
    }

    [Fact]
    public void LeadingDotOnNextLine_SuppressesNewline() {
        // result := xs\n  .filter() — the newline must be suppressed by the
        // leading-dot rule.
        IReadOnlyList<Token> tokens = Lex("xs\n  .filter()");
        AssertKinds(tokens,
            TokenKind.Identifier,
            TokenKind.Dot, TokenKind.Identifier, TokenKind.LeftParen, TokenKind.RightParen,
            TokenKind.Eof);
    }

    [Fact]
    public void TrailingArrow_SuppressesNewline() {
        AssertKinds(Lex("xs.map(x =>\n  x + 1)"),
            TokenKind.Identifier, TokenKind.Dot, TokenKind.Identifier, TokenKind.LeftParen,
            TokenKind.Identifier, TokenKind.Arrow,
            TokenKind.Identifier, TokenKind.Plus, TokenKind.IntLiteral,
            TokenKind.RightParen,
            TokenKind.Eof);
    }

    [Fact]
    public void Otherwise_NewlineIsKept() {
        AssertKinds(Lex("a\nb\n"),
            TokenKind.Identifier, TokenKind.Newline,
            TokenKind.Identifier, TokenKind.Newline,
            TokenKind.Eof);
    }

    [Fact]
    public void Crlf_ProducesSingleNewlineToken() {
        AssertKinds(Lex("a\r\nb"),
            TokenKind.Identifier, TokenKind.Newline, TokenKind.Identifier, TokenKind.Eof);
    }

    [Fact]
    public void BracketDepth_IncrementsInsidePairs() {
        IReadOnlyList<Token> tokens = Lex("f(a, b)");
        Token leftParen = tokens[1];
        Token argA = tokens[2];
        Token comma = tokens[3];
        Token rightParen = tokens[5];
        Assert.Equal(TokenKind.LeftParen, leftParen.Kind);
        Assert.Equal(0, leftParen.BracketDepth);
        Assert.Equal(1, argA.BracketDepth);
        Assert.Equal(1, comma.BracketDepth);
        Assert.Equal(0, rightParen.BracketDepth);
    }

    [Fact]
    public void BracketDepth_Nests() {
        IReadOnlyList<Token> tokens = Lex("[(x)]");
        Assert.Equal(TokenKind.LeftBracket, tokens[0].Kind);
        Assert.Equal(0, tokens[0].BracketDepth);
        Assert.Equal(TokenKind.LeftParen, tokens[1].Kind);
        Assert.Equal(1, tokens[1].BracketDepth);
        Assert.Equal(TokenKind.Identifier, tokens[2].Kind);
        Assert.Equal(2, tokens[2].BracketDepth);
        Assert.Equal(TokenKind.RightParen, tokens[3].Kind);
        Assert.Equal(1, tokens[3].BracketDepth);
        Assert.Equal(TokenKind.RightBracket, tokens[4].Kind);
        Assert.Equal(0, tokens[4].BracketDepth);
    }

    [Fact]
    public void Eof_IsAlwaysEmittedAtDepthZero() {
        // Mismatched ( leaves _depth > 0; EOF should still report 0.
        var (tokens, _) = LexWithDiagnostics("(a");
        Token eof = tokens[^1];
        Assert.Equal(TokenKind.Eof, eof.Kind);
        Assert.Equal(0, eof.BracketDepth);
    }

    [Fact]
    public void Interp_IncrementsDepthForInnerExpression() {
        IReadOnlyList<Token> tokens = Lex("\"${a}\"");
        Token ident = tokens.Single(t => t.Kind == TokenKind.Identifier);
        Assert.True(ident.BracketDepth > 0);
    }
}
