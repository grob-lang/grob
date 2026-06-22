using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerKeywordTests {
    public static IEnumerable<object[]> Keywords => [
        ["fn", TokenKind.Fn],
        ["if", TokenKind.If],
        ["else", TokenKind.Else],
        ["while", TokenKind.While],
        ["for", TokenKind.For],
        ["in", TokenKind.In],
        ["return", TokenKind.Return],
        ["const", TokenKind.Const],
        ["readonly", TokenKind.Readonly],
        ["type", TokenKind.Type],
        ["param", TokenKind.Param],
        ["import", TokenKind.Import],
        ["as", TokenKind.As],
        ["try", TokenKind.Try],
        ["catch", TokenKind.Catch],
        ["finally", TokenKind.Finally],
        ["throw", TokenKind.Throw],
        ["case", TokenKind.Case],
        ["default", TokenKind.Default],
        ["break", TokenKind.Break],
        ["continue", TokenKind.Continue],
        ["true", TokenKind.True],
        ["false", TokenKind.False],
        ["nil", TokenKind.Nil],
        ["step", TokenKind.Step],
        ["switch", TokenKind.Switch],
    ];

    [Theory]
    [MemberData(nameof(Keywords))]
    public void Keyword_LexesToItsKind(string lexeme, TokenKind expected) {
        Token tok = SingleToken(lexeme);
        Assert.Equal(expected, tok.Kind);
        Assert.Equal(lexeme, tok.Lexeme);
    }

    [Theory]
    [InlineData("print")]
    [InlineData("exit")]
    [InlineData("input")]
    public void BuiltInName_IsAnIdentifier(string name) {
        Token tok = SingleToken(name);
        Assert.Equal(TokenKind.Identifier, tok.Kind);
    }

    [Theory]
    [InlineData("select")]
    [InlineData("formatAs")]
    public void ReservedIdentifier_IsAnIdentifier(string name) {
        // D-320 / D-282: reserved identifiers lex as ordinary identifiers so they
        // stay legal as member names after '.'; the prohibition on binding them is
        // a type-checker rule (E1103), not a lexer one.
        Token tok = SingleToken(name);
        Assert.Equal(TokenKind.Identifier, tok.Kind);
        Assert.Equal(name, tok.Lexeme);
    }

    [Fact]
    public void Keywords_AreCaseSensitive() {
        Token tok = SingleToken("If");
        Assert.Equal(TokenKind.Identifier, tok.Kind);
    }

    [Fact]
    public void IdentifierWithKeywordPrefix_IsNotAKeyword() {
        Token tok = SingleToken("ifx");
        Assert.Equal(TokenKind.Identifier, tok.Kind);
        Assert.Equal("ifx", tok.Lexeme);
    }
}
