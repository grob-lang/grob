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
    public void Operator_LexesToItsKind(string lexeme, TokenKind expected) {
        var (tokens, diagnostics) = LexWithDiagnostics(lexeme);
        Assert.Empty(diagnostics.Diagnostics);
        Assert.Equal(2, tokens.Count);
        Token tok = tokens[0];
        Assert.Equal(expected, tok.Kind);
        Assert.Equal(lexeme, tok.Lexeme);
        Assert.Equal(TokenKind.Eof, tokens[1].Kind);
    }

    [Fact]
    public void PlusPlus_IsOneTokenNotTwo() {
        AssertKinds(Lex("++"), TokenKind.PlusPlus, TokenKind.Eof);
    }

    [Fact]
    public void PlusSpacePlus_IsTwoTokens() {
        AssertKinds(Lex("+ +"), TokenKind.Plus, TokenKind.Plus, TokenKind.Eof);
    }

    [Fact]
    public void MaximalMunch_PrefersPlusAssignOverPlusThenAssign() {
        AssertKinds(Lex("+="), TokenKind.PlusAssign, TokenKind.Eof);
    }

    [Fact]
    public void Arrow_DistinguishedFromAssignThenGreater() {
        AssertKinds(Lex("=>"), TokenKind.Arrow, TokenKind.Eof);
        AssertKinds(Lex("= >"), TokenKind.Assign, TokenKind.Greater, TokenKind.Eof);
    }

    [Fact]
    public void SingleAmpersand_IsDiagnosed() {
        var (tokens, diagnostics) = LexWithDiagnostics("&");
        Assert.NotEmpty(diagnostics.Errors);
        Assert.Equal(TokenKind.Error, tokens[0].Kind);
    }

    [Fact]
    public void SinglePipe_IsDiagnosed() {
        var (tokens, diagnostics) = LexWithDiagnostics("|");
        Assert.NotEmpty(diagnostics.Errors);
        Assert.Equal(TokenKind.Error, tokens[0].Kind);
    }

    [Fact]
    public void StrayHashWithoutBrace_IsDiagnosed() {
        var (tokens, diagnostics) = LexWithDiagnostics("#x");
        Assert.NotEmpty(diagnostics.Errors);
        Assert.Equal(TokenKind.Error, tokens[0].Kind);
    }

    [Fact]
    public void MatchedParens_EmitOpenAndClose() {
        AssertKinds(Lex("()"), TokenKind.LeftParen, TokenKind.RightParen, TokenKind.Eof);
    }

    [Fact]
    public void MatchedBrackets_EmitOpenAndClose() {
        AssertKinds(Lex("[]"), TokenKind.LeftBracket, TokenKind.RightBracket, TokenKind.Eof);
    }

    [Fact]
    public void MatchedBraces_EmitOpenAndClose() {
        AssertKinds(Lex("{}"), TokenKind.LeftBrace, TokenKind.RightBrace, TokenKind.Eof);
    }

    [Theory]
    [InlineData(")")]
    [InlineData("]")]
    [InlineData("}")]
    public void StrayCloser_IsDiagnosed(string lexeme) {
        var (tokens, diagnostics) = LexWithDiagnostics(lexeme);
        Assert.NotEmpty(diagnostics.Errors);
        Assert.Equal(TokenKind.Error, tokens[0].Kind);
    }
}
