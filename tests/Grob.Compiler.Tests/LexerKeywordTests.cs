using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerKeywordTests {
    public static IEnumerable<object[]> Keywords => new[] {
        new object[] { "fn", TokenKind.Fn },
        new object[] { "if", TokenKind.If },
        new object[] { "else", TokenKind.Else },
        new object[] { "while", TokenKind.While },
        new object[] { "for", TokenKind.For },
        new object[] { "in", TokenKind.In },
        new object[] { "return", TokenKind.Return },
        new object[] { "const", TokenKind.Const },
        new object[] { "readonly", TokenKind.Readonly },
        new object[] { "type", TokenKind.Type },
        new object[] { "param", TokenKind.Param },
        new object[] { "import", TokenKind.Import },
        new object[] { "as", TokenKind.As },
        new object[] { "try", TokenKind.Try },
        new object[] { "catch", TokenKind.Catch },
        new object[] { "finally", TokenKind.Finally },
        new object[] { "throw", TokenKind.Throw },
        new object[] { "select", TokenKind.Select },
        new object[] { "case", TokenKind.Case },
        new object[] { "default", TokenKind.Default },
        new object[] { "break", TokenKind.Break },
        new object[] { "continue", TokenKind.Continue },
        new object[] { "true", TokenKind.True },
        new object[] { "false", TokenKind.False },
        new object[] { "nil", TokenKind.Nil },
        new object[] { "step", TokenKind.Step },
        new object[] { "switch", TokenKind.Switch },
    };

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
