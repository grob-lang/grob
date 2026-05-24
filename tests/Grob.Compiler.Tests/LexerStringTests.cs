using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerStringTests {
    [Fact]
    public void PlainString_SegmentsIntoStartPartEnd() {
        IReadOnlyList<Token> tokens = Lex("\"hello\"");
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringPart, TokenKind.StringEnd, TokenKind.Eof);
        Assert.Equal("\"", tokens[0].Lexeme);
        Assert.Equal("hello", tokens[1].Lexeme);
        Assert.Equal("\"", tokens[2].Lexeme);
    }

    [Fact]
    public void EmptyString_HasNoPart() {
        IReadOnlyList<Token> tokens = Lex("\"\"");
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringEnd, TokenKind.Eof);
    }

    [Fact]
    public void StringWithInterpolation_SegmentsCorrectly() {
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
    public void StringStartingWithInterpolation_HasNoLeadingPart() {
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
    public void AdjacentInterpolations_EmitNoInterveningPart() {
        IReadOnlyList<Token> tokens = Lex("\"${a}${b}\"");
        AssertKinds(tokens,
            TokenKind.StringStart,
            TokenKind.InterpStart, TokenKind.Identifier, TokenKind.InterpEnd,
            TokenKind.InterpStart, TokenKind.Identifier, TokenKind.InterpEnd,
            TokenKind.StringEnd,
            TokenKind.Eof);
    }

    [Fact]
    public void InterpolationExpression_CanContainArbitraryTokens() {
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
    public void StringPart_KeepsRawEscapeSequencesInLexeme() {
        IReadOnlyList<Token> tokens = Lex("\"a\\nb\"");
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringPart, TokenKind.StringEnd, TokenKind.Eof);
        Assert.Equal("a\\nb", tokens[1].Lexeme);
    }

    [Fact]
    public void EscapedDollar_DoesNotOpenInterpolation() {
        IReadOnlyList<Token> tokens = Lex("\"price \\$5\"");
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringPart, TokenKind.StringEnd, TokenKind.Eof);
        Assert.Equal("price \\$5", tokens[1].Lexeme);
    }

    [Fact]
    public void NewlineInsideString_IsAnUnterminatedStringError() {
        var (tokens, diagnostics) = LexWithDiagnostics("\"oops\n");
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("E2002", diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(2, diag.Range.Start.Column);
        // The lexer recovers — synthesises StringEnd, then re-emits the newline.
        Assert.Contains(tokens, t => t.Kind == TokenKind.StringEnd);
    }

    [Fact]
    public void EofInsideString_IsAnUnterminatedStringError() {
        var (tokens, diagnostics) = LexWithDiagnostics("\"oops");
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("E2002", diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(2, diag.Range.Start.Column);
        Assert.Equal(TokenKind.Eof, tokens[^1].Kind);
    }

    [Fact]
    public void UnterminatedInterpolationAtEof_IsDiagnosed() {
        var (tokens, diagnostics) = LexWithDiagnostics("\"hi ${x");
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("E2009", diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        // Diagnostic points at EOF position, after the last consumed char.
        Assert.True(diag.Range.Start.Column > 1, $"expected column > 1, got {diag.Range.Start.Column}");
        // Stream is well-formed: ends with synthesised InterpEnd, StringEnd, Eof.
        Assert.Equal(TokenKind.Eof, tokens[^1].Kind);
        Assert.Equal(TokenKind.StringEnd, tokens[^2].Kind);
        Assert.Equal(TokenKind.InterpEnd, tokens[^3].Kind);
    }

    [Fact]
    public void UnknownEscape_ReportsDiagnosticButTokenStreamRemainsWellFormed() {
        var (tokens, diagnostics) = LexWithDiagnostics("\"\\q\"");
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("E2005", diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(2, diag.Range.Start.Column);
        AssertKinds(tokens, TokenKind.StringStart, TokenKind.StringPart, TokenKind.StringEnd, TokenKind.Eof);
    }

    [Fact]
    public void SingleBacktickString_IsOneRawStringLiteral() {
        Token tok = SingleToken("`C:\\Users\\chris`");
        Assert.Equal(TokenKind.RawStringLiteral, tok.Kind);
        Assert.Equal("`C:\\Users\\chris`", tok.Lexeme);
    }

    [Fact]
    public void SingleBacktickString_DoesNotProcessDollarBraces() {
        Token tok = SingleToken("`${not interp}`");
        Assert.Equal(TokenKind.RawStringLiteral, tok.Kind);
    }

    [Fact]
    public void NewlineInsideSingleBacktick_IsAnError() {
        var (_, diagnostics) = LexWithDiagnostics("`bad\nthing`");
        Assert.NotEmpty(diagnostics.Errors);
        Diagnostic first = diagnostics.Errors.First();
        Assert.Equal("E2004", first.Code);
        Assert.Equal(1, first.Range.Start.Line);
        Assert.True(first.Range.Start.Column >= 1);
    }

    [Fact]
    public void TripleBacktickBlock_PreservesNewlines() {
        const string Source = "```\nSELECT *\nFROM users\n```";
        Token tok = SingleToken(Source);
        Assert.Equal(TokenKind.RawStringBlockLiteral, tok.Kind);
        Assert.Equal(Source, tok.Lexeme);
    }

    [Fact]
    public void UnterminatedTripleBacktickBlock_ReportsDiagnostic() {
        var (_, diagnostics) = LexWithDiagnostics("```\nstill open");
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("E2004", diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }
}
