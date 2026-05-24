using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerRegexTests {
    [Fact]
    public void SlashAfterIdentifier_IsDivision() {
        AssertKinds(Lex("a / b"),
            TokenKind.Identifier, TokenKind.Slash, TokenKind.Identifier, TokenKind.Eof);
    }

    [Fact]
    public void SlashAtExpressionStart_IsRegex() {
        IReadOnlyList<Token> tokens = Lex("x := /^\\d+$/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal("/^\\d+$/", tokens[2].Lexeme);
    }

    [Fact]
    public void SlashAfterOpenParen_IsRegex() {
        IReadOnlyList<Token> tokens = Lex("match(/foo/)");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.LeftParen, TokenKind.RegexLiteral, TokenKind.RightParen, TokenKind.Eof);
    }

    [Fact]
    public void SlashAfterIntLiteral_IsDivision() {
        AssertKinds(Lex("4 / 2"),
            TokenKind.IntLiteral, TokenKind.Slash, TokenKind.IntLiteral, TokenKind.Eof);
    }

    [Fact]
    public void Regex_WithTrailingFlags() {
        IReadOnlyList<Token> tokens = Lex("x := /abc/i");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal("/abc/i", tokens[2].Lexeme);
    }

    [Fact]
    public void RegexWithInvalidFlag_ReportsDiagnostic() {
        var (tokens, diagnostics) = LexWithDiagnostics("x := /abc/z");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal("/abc/z", tokens[2].Lexeme);
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("E2007", diag.Code);
    }

    [Fact]
    public void EscapedSlashInsideRegex_DoesNotCloseIt() {
        IReadOnlyList<Token> tokens = Lex("x := /a\\/b/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal("/a\\/b/", tokens[2].Lexeme);
    }

    [Fact]
    public void UnterminatedRegex_ReportsDiagnostic() {
        var (tokens, diagnostics) = LexWithDiagnostics("x := /unterminated\n");
        Assert.Contains(tokens, t => t.Kind == TokenKind.Error);
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("E2008", diag.Code);
    }
}
