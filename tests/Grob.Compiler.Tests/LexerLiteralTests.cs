using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerLiteralTests {
    [Theory]
    [InlineData("42")]
    [InlineData("0")]
    [InlineData("1_000_000")]
    public void Decimal_int_literal(string source) {
        Token tok = SingleToken(source);
        Assert.Equal(TokenKind.IntLiteral, tok.Kind);
        Assert.Equal(source, tok.Lexeme);
        Assert.False(tok.Lexeme.StartsWith('0') && tok.Lexeme.Length > 1 &&
            (tok.Lexeme[1] is 'x' or 'X' or 'b' or 'B'));
    }

    [Theory]
    [InlineData("0xFF")]
    [InlineData("0xff")]
    [InlineData("0xFF_FF")]
    [InlineData("0X10")]
    public void Hex_int_literal(string source) {
        Token tok = SingleToken(source);
        Assert.Equal(TokenKind.IntLiteral, tok.Kind);
        Assert.Equal(source, tok.Lexeme);
        Assert.StartsWith("0x", tok.Lexeme, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("0b1010")]
    [InlineData("0B1010_0101")]
    public void Binary_int_literal(string source) {
        Token tok = SingleToken(source);
        Assert.Equal(TokenKind.IntLiteral, tok.Kind);
        Assert.Equal(source, tok.Lexeme);
        Assert.StartsWith("0b", tok.Lexeme, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("3.14")]
    [InlineData("0.5")]
    [InlineData("1_000.50")]
    public void Float_literal(string source) {
        Token tok = SingleToken(source);
        Assert.Equal(TokenKind.FloatLiteral, tok.Kind);
        Assert.Equal(source, tok.Lexeme);
    }

    [Fact]
    public void Leading_dot_is_not_a_float() {
        // ".5" must lex as Dot then IntLiteral(5), not as a float — the spec
        // requires a leading digit on float literals.
        AssertKinds(Lex(".5"), TokenKind.Dot, TokenKind.IntLiteral, TokenKind.Eof);
    }

    [Fact]
    public void Int_followed_by_range_is_two_tokens() {
        // 1..10 — IntLiteral(1) DotDot IntLiteral(10), not a float.
        AssertKinds(Lex("1..10"),
            TokenKind.IntLiteral, TokenKind.DotDot, TokenKind.IntLiteral, TokenKind.Eof);
    }

    [Fact]
    public void Hex_literal_with_no_digits_reports_diagnostic() {
        var (tokens, diagnostics) = LexWithDiagnostics("0x");
        Assert.Single(diagnostics.Errors);
        Assert.Equal(TokenKind.IntLiteral, tokens[0].Kind);
    }

    [Fact]
    public void Binary_literal_with_no_digits_reports_diagnostic() {
        var (tokens, diagnostics) = LexWithDiagnostics("0b");
        Assert.Single(diagnostics.Errors);
        Assert.Equal(TokenKind.IntLiteral, tokens[0].Kind);
    }

    [Fact]
    public void Member_access_on_int_is_not_a_float() {
        // "x.length" — Identifier Dot Identifier. Not relevant here, but the
        // analogue "5.foo" must also lex as IntLiteral Dot Identifier.
        AssertKinds(Lex("5.foo"),
            TokenKind.IntLiteral, TokenKind.Dot, TokenKind.Identifier, TokenKind.Eof);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("name")]
    [InlineData("_under")]
    [InlineData("user1")]
    [InlineData("camelCase")]
    public void Identifier_lexes(string source) {
        Token tok = SingleToken(source);
        Assert.Equal(TokenKind.Identifier, tok.Kind);
        Assert.Equal(source, tok.Lexeme);
    }
}
