using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerRegexTests {
    [Fact]
    public void Slash_after_identifier_is_division() {
        AssertKinds(Lex("a / b"),
            TokenKind.Identifier, TokenKind.Slash, TokenKind.Identifier, TokenKind.Eof);
    }

    [Fact]
    public void Slash_at_expression_start_is_regex() {
        IReadOnlyList<Token> tokens = Lex("x := /^\\d+$/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal("/^\\d+$/", tokens[2].Lexeme);
    }

    [Fact]
    public void Slash_after_open_paren_is_regex() {
        IReadOnlyList<Token> tokens = Lex("match(/foo/)");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.LeftParen, TokenKind.RegexLiteral, TokenKind.RightParen, TokenKind.Eof);
    }

    [Fact]
    public void Slash_after_int_literal_is_division() {
        AssertKinds(Lex("4 / 2"),
            TokenKind.IntLiteral, TokenKind.Slash, TokenKind.IntLiteral, TokenKind.Eof);
    }

    [Fact]
    public void Regex_with_trailing_flags() {
        IReadOnlyList<Token> tokens = Lex("x := /abc/i");
        Assert.Equal(TokenKind.RegexLiteral, tokens[2].Kind);
        Assert.Equal("/abc/i", tokens[2].Lexeme);
    }

    [Fact]
    public void Escaped_slash_inside_regex_does_not_close_it() {
        IReadOnlyList<Token> tokens = Lex("x := /a\\/b/");
        Assert.Equal(TokenKind.RegexLiteral, tokens[2].Kind);
        Assert.Equal("/a\\/b/", tokens[2].Lexeme);
    }

    [Fact]
    public void Unterminated_regex_reports_diagnostic() {
        var (_, diagnostics) = LexWithDiagnostics("x := /unterminated\n");
        Assert.NotEmpty(diagnostics.Errors);
    }
}
