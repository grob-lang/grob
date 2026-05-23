using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerStringTests {
    [Fact]
    public void Plain_string_segments_into_start_part_end() {
        IReadOnlyList<Token> tokens = Lex("\"hello\"");
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringPart, TokenKind.StringEnd, TokenKind.Eof);
        Assert.Equal("\"", tokens[0].Lexeme);
        Assert.Equal("hello", tokens[1].Lexeme);
        Assert.Equal("\"", tokens[2].Lexeme);
    }

    [Fact]
    public void Empty_string_has_no_part() {
        IReadOnlyList<Token> tokens = Lex("\"\"");
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringEnd, TokenKind.Eof);
    }

    [Fact]
    public void String_with_interpolation_segments_correctly() {
        IReadOnlyList<Token> tokens = Lex("\"hi ${name}!\"");
        AssertKinds(tokens,
            TokenKind.StringStart,
            TokenKind.StringPart,
            TokenKind.InterpStart,
            TokenKind.Identifier,
            TokenKind.InterpEnd,
            TokenKind.StringPart,
            TokenKind.StringEnd,
            TokenKind.Eof);
        Assert.Equal("hi ", tokens[1].Lexeme);
        Assert.Equal("${", tokens[2].Lexeme);
        Assert.Equal("name", tokens[3].Lexeme);
        Assert.Equal("}", tokens[4].Lexeme);
        Assert.Equal("!", tokens[5].Lexeme);
    }

    [Fact]
    public void String_starting_with_interpolation_has_no_leading_part() {
        IReadOnlyList<Token> tokens = Lex("\"${name}\"");
        AssertKinds(tokens,
            TokenKind.StringStart,
            TokenKind.InterpStart,
            TokenKind.Identifier,
            TokenKind.InterpEnd,
            TokenKind.StringEnd,
            TokenKind.Eof);
    }

    [Fact]
    public void Adjacent_interpolations_emit_no_intervening_part() {
        IReadOnlyList<Token> tokens = Lex("\"${a}${b}\"");
        AssertKinds(tokens,
            TokenKind.StringStart,
            TokenKind.InterpStart, TokenKind.Identifier, TokenKind.InterpEnd,
            TokenKind.InterpStart, TokenKind.Identifier, TokenKind.InterpEnd,
            TokenKind.StringEnd,
            TokenKind.Eof);
    }

    [Fact]
    public void Interpolation_expression_can_contain_arbitrary_tokens() {
        IReadOnlyList<Token> tokens = Lex("\"v=${a + 1}\"");
        AssertKinds(tokens,
            TokenKind.StringStart,
            TokenKind.StringPart,
            TokenKind.InterpStart,
            TokenKind.Identifier, TokenKind.Plus, TokenKind.IntLiteral,
            TokenKind.InterpEnd,
            TokenKind.StringEnd,
            TokenKind.Eof);
    }

    [Fact]
    public void String_part_keeps_raw_escape_sequences_in_lexeme() {
        IReadOnlyList<Token> tokens = Lex("\"a\\nb\"");
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringPart, TokenKind.StringEnd, TokenKind.Eof);
        Assert.Equal("a\\nb", tokens[1].Lexeme);
    }

    [Fact]
    public void Escaped_dollar_does_not_open_interpolation() {
        IReadOnlyList<Token> tokens = Lex("\"price \\$5\"");
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringPart, TokenKind.StringEnd, TokenKind.Eof);
        Assert.Equal("price \\$5", tokens[1].Lexeme);
    }

    [Fact]
    public void Newline_inside_string_is_an_unterminated_string_error() {
        var (tokens, diagnostics) = LexWithDiagnostics("\"oops\n");
        Assert.Single(diagnostics.Errors);
        // The lexer recovers — synthesises StringEnd, then re-emits the newline.
        Assert.Contains(tokens, t => t.Kind == TokenKind.StringEnd);
    }

    [Fact]
    public void Eof_inside_string_is_an_unterminated_string_error() {
        var (tokens, diagnostics) = LexWithDiagnostics("\"oops");
        Assert.Single(diagnostics.Errors);
        Assert.Equal(TokenKind.Eof, tokens[^1].Kind);
    }

    [Fact]
    public void Unknown_escape_reports_diagnostic_but_token_stream_remains_well_formed() {
        var (tokens, diagnostics) = LexWithDiagnostics("\"\\q\"");
        Assert.Single(diagnostics.Errors);
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringPart, TokenKind.StringEnd, TokenKind.Eof);
    }

    [Fact]
    public void Single_backtick_string_is_one_raw_string_literal() {
        Token tok = SingleToken("`C:\\Users\\chris`");
        Assert.Equal(TokenKind.RawStringLiteral, tok.Kind);
        Assert.Equal("`C:\\Users\\chris`", tok.Lexeme);
    }

    [Fact]
    public void Single_backtick_string_does_not_process_dollar_braces() {
        Token tok = SingleToken("`${not interp}`");
        Assert.Equal(TokenKind.RawStringLiteral, tok.Kind);
    }

    [Fact]
    public void Newline_inside_single_backtick_is_an_error() {
        var (_, diagnostics) = LexWithDiagnostics("`bad\nthing`");
        Assert.NotEmpty(diagnostics.Errors);
    }

    [Fact]
    public void Triple_backtick_block_preserves_newlines() {
        const string Source = "```\nSELECT *\nFROM users\n```";
        Token tok = SingleToken(Source);
        Assert.Equal(TokenKind.RawStringBlockLiteral, tok.Kind);
        Assert.Equal(Source, tok.Lexeme);
    }

    [Fact]
    public void Unterminated_triple_backtick_block_reports_diagnostic() {
        var (_, diagnostics) = LexWithDiagnostics("```\nstill open");
        Assert.NotEmpty(diagnostics.Errors);
    }
}
