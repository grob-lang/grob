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
        ["select", TokenKind.Select],
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
    public void Keyword_lexes_to_its_kind(string lexeme, TokenKind expected) {
        Token tok = SingleToken(lexeme);
        Assert.Equal(expected, tok.Kind);
        Assert.Equal(lexeme, tok.Lexeme);
    }

    [Theory]
    [InlineData("print")]
    [InlineData("exit")]
    [InlineData("input")]
    public void Built_in_name_is_an_identifier(string name) {
        Token tok = SingleToken(name);
        Assert.Equal(TokenKind.Identifier, tok.Kind);
    }

    [Fact]
    public void Keywords_are_case_sensitive() {
        Token tok = SingleToken("If");
        Assert.Equal(TokenKind.Identifier, tok.Kind);
    }

    [Fact]
    public void Identifier_with_keyword_prefix_is_not_a_keyword() {
        Token tok = SingleToken("ifx");
        Assert.Equal(TokenKind.Identifier, tok.Kind);
        Assert.Equal("ifx", tok.Lexeme);
    }
}
