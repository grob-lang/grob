using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerOperatorTests {
    public static IEnumerable<object[]> Operators => [
        ["+", TokenKind.Plus],
        ["-", TokenKind.Minus],
        ["*", TokenKind.Star],
        ["%", TokenKind.Percent],
        ["=", TokenKind.Assign],
        [":=", TokenKind.ColonAssign],
        ["==", TokenKind.EqualEqual],
        ["!=", TokenKind.BangEqual],
        ["<", TokenKind.Less],
        [">", TokenKind.Greater],
        ["<=", TokenKind.LessEqual],
        [">=", TokenKind.GreaterEqual],
        ["!", TokenKind.Bang],
        ["&&", TokenKind.AmpAmp],
        ["||", TokenKind.PipePipe],
        ["?", TokenKind.Question],
        [":", TokenKind.Colon],
        ["??", TokenKind.QuestionQuestion],
        ["?.", TokenKind.QuestionDot],
        ["+=", TokenKind.PlusAssign],
        ["-=", TokenKind.MinusAssign],
        ["*=", TokenKind.StarAssign],
        ["/=", TokenKind.SlashAssign],
        ["%=", TokenKind.PercentAssign],
        ["++", TokenKind.PlusPlus],
        ["--", TokenKind.MinusMinus],
        ["..", TokenKind.DotDot],
        ["=>", TokenKind.Arrow],
        ["(", TokenKind.LeftParen],
        ["{", TokenKind.LeftBrace],
        ["[", TokenKind.LeftBracket],
        [",", TokenKind.Comma],
        [".", TokenKind.Dot],
        ["#{", TokenKind.HashBrace],
        ["@", TokenKind.At],
    ];

    [Theory]
    [MemberData(nameof(Operators))]
    public void Operator_lexes_to_its_kind(string lexeme, TokenKind expected) {
        var (tokens, diagnostics) = LexWithDiagnostics(lexeme);
        Assert.Empty(diagnostics.Diagnostics);
        Assert.Equal(2, tokens.Count);
        Token tok = tokens[0];
        Assert.Equal(expected, tok.Kind);
        Assert.Equal(lexeme, tok.Lexeme);
        Assert.Equal(TokenKind.Eof, tokens[1].Kind);
    }

    [Fact]
    public void Plus_plus_is_one_token_not_two() {
        AssertKinds(Lex("++"), TokenKind.PlusPlus, TokenKind.Eof);
    }

    [Fact]
    public void Plus_space_plus_is_two_tokens() {
        AssertKinds(Lex("+ +"), TokenKind.Plus, TokenKind.Plus, TokenKind.Eof);
    }

    [Fact]
    public void Maximal_munch_prefers_PlusAssign_over_Plus_then_Assign() {
        AssertKinds(Lex("+="), TokenKind.PlusAssign, TokenKind.Eof);
    }

    [Fact]
    public void Arrow_distinguished_from_Assign_then_Greater() {
        AssertKinds(Lex("=>"), TokenKind.Arrow, TokenKind.Eof);
        AssertKinds(Lex("= >"), TokenKind.Assign, TokenKind.Greater, TokenKind.Eof);
    }

    [Fact]
    public void Single_ampersand_is_diagnosed() {
        var (tokens, diagnostics) = LexWithDiagnostics("&");
        Assert.NotEmpty(diagnostics.Errors);
        Assert.Equal(TokenKind.Error, tokens[0].Kind);
    }

    [Fact]
    public void Single_pipe_is_diagnosed() {
        var (tokens, diagnostics) = LexWithDiagnostics("|");
        Assert.NotEmpty(diagnostics.Errors);
        Assert.Equal(TokenKind.Error, tokens[0].Kind);
    }

    [Fact]
    public void Stray_hash_without_brace_is_diagnosed() {
        var (tokens, diagnostics) = LexWithDiagnostics("#x");
        Assert.NotEmpty(diagnostics.Errors);
        Assert.Equal(TokenKind.Error, tokens[0].Kind);
    }

    [Fact]
    public void Matched_parens_emit_open_and_close() {
        AssertKinds(Lex("()"), TokenKind.LeftParen, TokenKind.RightParen, TokenKind.Eof);
    }

    [Fact]
    public void Matched_brackets_emit_open_and_close() {
        AssertKinds(Lex("[]"), TokenKind.LeftBracket, TokenKind.RightBracket, TokenKind.Eof);
    }

    [Fact]
    public void Matched_braces_emit_open_and_close() {
        AssertKinds(Lex("{}"), TokenKind.LeftBrace, TokenKind.RightBrace, TokenKind.Eof);
    }

    [Theory]
    [InlineData(")")]
    [InlineData("]")]
    [InlineData("}")]
    public void Stray_closer_is_diagnosed(string lexeme) {
        var (tokens, diagnostics) = LexWithDiagnostics(lexeme);
        Assert.NotEmpty(diagnostics.Errors);
        Assert.Equal(TokenKind.Error, tokens[0].Kind);
    }
}
