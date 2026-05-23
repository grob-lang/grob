using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerOperatorTests {
    public static IEnumerable<object[]> Operators => new[] {
        new object[] { "+", TokenKind.Plus },
        new object[] { "-", TokenKind.Minus },
        new object[] { "*", TokenKind.Star },
        new object[] { "%", TokenKind.Percent },
        new object[] { "=", TokenKind.Assign },
        new object[] { ":=", TokenKind.ColonAssign },
        new object[] { "==", TokenKind.EqualEqual },
        new object[] { "!=", TokenKind.BangEqual },
        new object[] { "<", TokenKind.Less },
        new object[] { ">", TokenKind.Greater },
        new object[] { "<=", TokenKind.LessEqual },
        new object[] { ">=", TokenKind.GreaterEqual },
        new object[] { "!", TokenKind.Bang },
        new object[] { "&&", TokenKind.AmpAmp },
        new object[] { "||", TokenKind.PipePipe },
        new object[] { "?", TokenKind.Question },
        new object[] { ":", TokenKind.Colon },
        new object[] { "??", TokenKind.QuestionQuestion },
        new object[] { "?.", TokenKind.QuestionDot },
        new object[] { "+=", TokenKind.PlusAssign },
        new object[] { "-=", TokenKind.MinusAssign },
        new object[] { "*=", TokenKind.StarAssign },
        new object[] { "/=", TokenKind.SlashAssign },
        new object[] { "%=", TokenKind.PercentAssign },
        new object[] { "++", TokenKind.PlusPlus },
        new object[] { "--", TokenKind.MinusMinus },
        new object[] { "..", TokenKind.DotDot },
        new object[] { "=>", TokenKind.Arrow },
        new object[] { "(", TokenKind.LeftParen },
        new object[] { "{", TokenKind.LeftBrace },
        new object[] { "[", TokenKind.LeftBracket },
        new object[] { ",", TokenKind.Comma },
        new object[] { ".", TokenKind.Dot },
        new object[] { "#{", TokenKind.HashBrace },
        new object[] { "@", TokenKind.At },
    };

    [Theory]
    [MemberData(nameof(Operators))]
    public void Operator_lexes_to_its_kind(string lexeme, TokenKind expected) {
        var (tokens, diagnostics) = LexWithDiagnostics(lexeme);
        Assert.Empty(diagnostics.Diagnostics);
        Token tok = tokens[0];
        Assert.Equal(expected, tok.Kind);
        Assert.Equal(lexeme, tok.Lexeme);
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
