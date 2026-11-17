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

    /// <summary>
    /// D-432 Decision 3, implemented by D-440: E2007 points a flag other engines spell as
    /// a flag at Grob's way of saying it. There is no <c>g</c>; <c>s</c> and <c>x</c> go
    /// inline at the start of the pattern.
    /// </summary>
    [Theory]
    [InlineData("x := /abc/g", "use matchAll or replaceAll for every match")]
    [InlineData("x := /abc/s", "write (?s) at the start of the pattern")]
    [InlineData("x := /abc/x", "write (?x) at the start of the pattern")]
    public void RegexFlagFromAnotherEngine_ReportsE2007WithAHint(string source, string hint) {
        var (_, diagnostics) = LexWithDiagnostics(source);
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("E2007", diag.Code);
        Assert.Contains(hint, diag.Message);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(11, diag.Range.Start.Column);
    }

    [Fact]
    public void RegexFlagWithNoHint_ReportsE2007Plainly() {
        var (_, diagnostics) = LexWithDiagnostics("x := /abc/z");
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("invalid regex flag 'z' (only 'i' and 'm' are supported)", diag.Message);
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

    // -----------------------------------------------------------------
    // D-432 — the completed `/` rule (fundamentals §8, "Division or a regex literal")
    // -----------------------------------------------------------------

    [Fact]
    public void SlashEqualsAfterOpenParen_IsRegexBeginningWithEquals() {
        // §8 table: `split(/=+/)` is a regex — `(` is before, so `/=` is not compound division.
        IReadOnlyList<Token> tokens = Lex("split(/=+/)");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.LeftParen, TokenKind.RegexLiteral, TokenKind.RightParen, TokenKind.Eof);
        Assert.Equal("/=+/", tokens[2].Lexeme);
    }

    [Fact]
    public void SlashEqualsAfterAssign_IsRegexBeginningWithEquals() {
        IReadOnlyList<Token> tokens = Lex("x := /=/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal("/=/", tokens[2].Lexeme);
    }

    [Fact]
    public void SlashEqualsAfterIdentifier_IsCompoundDivision() {
        AssertKinds(Lex("x /= 2"),
            TokenKind.Identifier, TokenKind.SlashAssign, TokenKind.IntLiteral, TokenKind.Eof);
    }

    [Fact]
    public void SlashEqualsAfterCloseParen_IsCompoundDivision() {
        AssertKinds(Lex("f() /= 2"),
            TokenKind.Identifier, TokenKind.LeftParen, TokenKind.RightParen,
            TokenKind.SlashAssign, TokenKind.IntLiteral, TokenKind.Eof);
    }

    [Fact]
    public void SlashBeginningLine_AfterStatementNewline_IsRegex() {
        // §8: a `/` after a statement-terminating newline cannot be division.
        IReadOnlyList<Token> tokens = Lex("x := 1\n/abc/.isMatch(s)");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.IntLiteral, TokenKind.Newline,
            TokenKind.RegexLiteral, TokenKind.Dot, TokenKind.Identifier,
            TokenKind.LeftParen, TokenKind.Identifier, TokenKind.RightParen, TokenKind.Eof);
        Assert.Equal("/abc/", tokens[4].Lexeme);
    }

    [Fact]
    public void SlashBeginningLine_AfterIdentifierStatement_IsRegex() {
        IReadOnlyList<Token> tokens = Lex("print(s)\n/^x/.isMatch(s)");
        Assert.Contains(tokens, t => t.Kind == TokenKind.RegexLiteral);
        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.Slash);
    }

    [Fact]
    public void SlashBeginningLine_InsideParentheses_IsDivision() {
        // §8 table: `total := (a` newline `/ b)` — the newline is suppressed inside `(`.
        // The division's leading newline is dropped (§14), so the parser sees one expression.
        AssertKinds(Lex("total := (a\n/ b)"),
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.LeftParen, TokenKind.Identifier,
            TokenKind.Slash, TokenKind.Identifier, TokenKind.RightParen, TokenKind.Eof);
    }

    [Fact]
    public void SlashBeginningLine_InsideSquareBrackets_IsDivision() {
        AssertKinds(Lex("xs[a\n/ 2]"),
            TokenKind.Identifier, TokenKind.LeftBracket, TokenKind.Identifier,
            TokenKind.Slash, TokenKind.IntLiteral, TokenKind.RightBracket, TokenKind.Eof);
    }

    [Fact]
    public void SlashBeginningLine_ParenthesesInsideBlock_DropsTheDivisionNewline() {
        AssertKinds(Lex("if ok {\n    z := (a\n    / b)\n}"),
            TokenKind.If, TokenKind.Identifier, TokenKind.LeftBrace,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.LeftParen, TokenKind.Identifier,
            TokenKind.Slash, TokenKind.Identifier, TokenKind.RightParen, TokenKind.Newline,
            TokenKind.RightBrace, TokenKind.Eof);
    }

    [Fact]
    public void SlashBeginningLine_AfterStatementNewline_KeepsTheNewline() {
        // Only a division's newline is dropped; a regex at the start of a line keeps the
        // statement boundary before it.
        IReadOnlyList<Token> tokens = Lex("x := 1\n/a/");
        Assert.Contains(tokens, t => t.Kind == TokenKind.Newline);
    }

    [Fact]
    public void SlashBeginningLine_InsideBlockBraces_IsRegex() {
        // A block's newlines still terminate statements even though the bracket depth is non-zero.
        IReadOnlyList<Token> tokens = Lex("if ok {\n    y := 1\n    /a/.isMatch(s)\n}");
        Assert.Single(tokens, t => t.Kind == TokenKind.RegexLiteral);
        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.Slash);
    }

    [Fact]
    public void SlashBeginningLine_InsideBlockBracesWithinParentheses_IsRegex() {
        // The innermost open delimiter decides: a `{` inside `(` is statement context again.
        IReadOnlyList<Token> tokens = Lex("run(() => {\n    y := 1\n    /a/.isMatch(s)\n})");
        Assert.Single(tokens, t => t.Kind == TokenKind.RegexLiteral);
    }

    [Fact]
    public void SlashAfterTrailingOperatorAcrossNewline_IsRegex() {
        IReadOnlyList<Token> tokens = Lex("x := a ??\n/b/");
        Assert.Single(tokens, t => t.Kind == TokenKind.RegexLiteral);
    }

    [Fact]
    public void SlashAfterKeyword_IsRegex() {
        IReadOnlyList<Token> tokens = Lex("return /^x/");
        AssertKinds(tokens, TokenKind.Return, TokenKind.RegexLiteral, TokenKind.Eof);
    }

    [Fact]
    public void SlashAfterIf_IsRegex() {
        IReadOnlyList<Token> tokens = Lex("if /^\\d+$/.isMatch(s) {\n}");
        Assert.Single(tokens, t => t.Kind == TokenKind.RegexLiteral);
    }

    [Fact]
    public void SlashAfterInterpolationStart_IsRegex() {
        IReadOnlyList<Token> tokens = Lex("\"${/x/.isMatch(s)}\"");
        Assert.Single(tokens, t => t.Kind == TokenKind.RegexLiteral);
    }

    [Fact]
    public void SlashAfterCloseBracketAndCloseParen_IsDivision() {
        AssertKinds(Lex("xs[0] / 2"),
            TokenKind.Identifier, TokenKind.LeftBracket, TokenKind.IntLiteral, TokenKind.RightBracket,
            TokenKind.Slash, TokenKind.IntLiteral, TokenKind.Eof);
        AssertKinds(Lex("(a + b) / 2"),
            TokenKind.LeftParen, TokenKind.Identifier, TokenKind.Plus, TokenKind.Identifier, TokenKind.RightParen,
            TokenKind.Slash, TokenKind.IntLiteral, TokenKind.Eof);
    }

    [Fact]
    public void DoubleSlash_AlwaysOpensComment() {
        AssertKinds(Lex("x := //"),
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.Eof);
    }

    // -----------------------------------------------------------------
    // D-432 — an unescaped `/` inside `[...]` does not end the literal
    // -----------------------------------------------------------------

    [Fact]
    public void SlashInsideCharacterClass_DoesNotEndLiteral() {
        IReadOnlyList<Token> tokens = Lex(@"x := /[^/]+$/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/[^/]+$/", tokens[2].Lexeme);
    }

    [Fact]
    public void SlashInsideCharacterClass_FlagsStillFollowTheClosingSlash() {
        IReadOnlyList<Token> tokens = Lex(@"x := /[a/b]+/i");
        Assert.Equal(@"/[a/b]+/i", tokens[2].Lexeme);
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
    }

    [Fact]
    public void CharacterClassClosed_SlashAfterItEndsLiteral() {
        IReadOnlyList<Token> tokens = Lex(@"x := /[ab]/");
        Assert.Equal(@"/[ab]/", tokens[2].Lexeme);
    }

    [Fact]
    public void EscapedOpenBracket_DoesNotStartCharacterClass() {
        // `\[` is a literal bracket, so the next `/` closes the literal.
        IReadOnlyList<Token> tokens = Lex(@"x := /\[/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/\[/", tokens[2].Lexeme);
    }

    [Fact]
    public void EscapedCloseBracketInsideClass_DoesNotEndClass() {
        IReadOnlyList<Token> tokens = Lex(@"x := /[\]/]+/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/[\]/]+/", tokens[2].Lexeme);
    }

    [Fact]
    public void CloseBracketFirstInClass_IsClassContent() {
        // .NET reads a `]` first in a class as content, so the `/` after it is still inside.
        IReadOnlyList<Token> tokens = Lex(@"x := /[]/]/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/[]/]/", tokens[2].Lexeme);
    }

    [Fact]
    public void CloseBracketFirstInNegatedClass_IsClassContent() {
        IReadOnlyList<Token> tokens = Lex(@"x := /[^]/]/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/[^]/]/", tokens[2].Lexeme);
    }

    [Fact]
    public void CloseBracketAfterCaretContent_ClosesClass() {
        // Only one `^` is the negation; a second is content, so the `]` after it closes the class.
        IReadOnlyList<Token> tokens = Lex(@"x := /[^^]/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/[^^]/", tokens[2].Lexeme);
    }

    [Fact]
    public void OpenBracketInsideInlineComment_DoesNotOpenClass() {
        // A `(?#...)` comment's text is not pattern, so its `[` opens no class.
        IReadOnlyList<Token> tokens = Lex(@"x := /(?# [)a/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/(?# [)a/", tokens[2].Lexeme);
    }

    [Fact]
    public void ClassAfterInlineComment_StillHoldsSlash() {
        IReadOnlyList<Token> tokens = Lex(@"x := /(?#[)[/]/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/(?#[)[/]/", tokens[2].Lexeme);
    }

    [Fact]
    public void BackslashInsideInlineComment_DoesNotEscapeTheClosingParen() {
        // .NET ends `(?#...)` at the first `)`, even after a backslash, so the class that
        // follows still holds its `/`.
        IReadOnlyList<Token> tokens = Lex(@"x := /(?#\)[/]/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/(?#\)[/]/", tokens[2].Lexeme);
    }

    [Fact]
    public void EscapedSlashInsideInlineComment_DoesNotEndLiteral() {
        // §8: `\/` writes a literal `/` anywhere, a comment included.
        IReadOnlyList<Token> tokens = Lex(@"x := /(?#a\/b)c/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/(?#a\/b)c/", tokens[2].Lexeme);
    }

    [Fact]
    public void InlineCommentOpenerInsideClass_IsClassContent() {
        // Inside a class `(?#` is three literal characters, so the `]` still closes it.
        IReadOnlyList<Token> tokens = Lex(@"x := /[(?#]/");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.RegexLiteral, TokenKind.Eof);
        Assert.Equal(@"/[(?#]/", tokens[2].Lexeme);
    }

    [Fact]
    public void UnclosedCharacterClass_RunsToNewlineAndReportsUnterminated() {
        // The `/` after `[abc` sits inside the class, so the literal never closes.
        var (tokens, diagnostics) = LexWithDiagnostics("x := /[abc/\n");
        Assert.Contains(tokens, t => t.Kind == TokenKind.Error);
        Diagnostic diag = Assert.Single(diagnostics.Errors);
        Assert.Equal("E2008", diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    [Fact]
    public void NewlineInsideCharacterClass_ReportsUnterminatedAtOpeningSlash() {
        // The stray `b]/` on line 2 produces its own diagnostics; the E2008 is the first.
        var (_, diagnostics) = LexWithDiagnostics("x := /[a\nb]/");
        Diagnostic diag = diagnostics.Errors.First(d => d.Code == "E2008");
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }
}
