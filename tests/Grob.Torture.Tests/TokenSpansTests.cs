using Grob.Compiler;
using Grob.Core;
using Grob.Torture.Mutation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// Where tokens sit in the source, and how far an expression or a type extends from a token. The
/// extent comes from the tokens because a node's <c>Range.End</c> is not its end (R-74).
/// </summary>
public sealed class TokenSpansTests {
    private static TokenSpans Spans(string source) => new(source, Lexer.Scan(source, new DiagnosticBag(), "t.grob"));

    private static int IndexOf(TokenSpans spans, TokenKind kind, int occurrence = 0) {
        int found = -1;
        for (int i = 0; i <= occurrence; i++) {
            found = spans.Find(found + 1, kind);
        }
        return found;
    }

    [Fact]
    public void Start_AndEnd_GiveTheRawTextOfASimpleToken() {
        TokenSpans spans = Spans("count := 10\n");

        Assert.Equal((0, 5), (spans.Start(0), spans.End(0)));
        Assert.Equal("10", spans.Text(spans.Start(2), spans.End(2)));
    }

    [Fact]
    public void End_StringWithAnEscape_RunsToTheClosingQuote() {
        const string source = "s := \"a\\tb\"\n";
        TokenSpans spans = Spans(source);
        int part = IndexOf(spans, TokenKind.StringPart);

        Assert.Equal("a\\tb", spans.Text(spans.Start(part), spans.End(part)));
    }

    [Fact]
    public void End_NumberWithSeparators_RunsToTheNextToken() {
        TokenSpans spans = Spans("n := 1_000 + 2\n");
        int literal = IndexOf(spans, TokenKind.IntLiteral);

        Assert.Equal("1_000", spans.Text(spans.Start(literal), spans.End(literal)));
    }

    [Fact]
    public void End_RawString_IncludesItsBackticks() {
        // The lexeme of a raw string leaves out the backticks, so it does not match the source and
        // the token runs to the next one, less the whitespace between.
        TokenSpans spans = Spans("p := `C:\\temp`  // note\n");
        int literal = IndexOf(spans, TokenKind.RawStringLiteral);

        Assert.Equal("`C:\\temp`", spans.Text(spans.Start(literal), spans.End(literal)));
    }

    [Fact]
    public void End_RegexLiteral_IncludesItsSlashes() {
        TokenSpans spans = Spans("r := /a+b/i\n");
        int literal = IndexOf(spans, TokenKind.RegexLiteral);

        Assert.Equal("/a+b/i", spans.Text(spans.Start(literal), spans.End(literal)));
    }

    [Fact]
    public void Constructor_TokenBeyondTheSource_HasNoOffset() {
        IReadOnlyList<Token> tokens = Lexer.Scan("a := 1\nb := 2\nc := 3\n", new DiagnosticBag(), "t.grob");

        var spans = new TokenSpans("a := 1\n", tokens);

        int beyond = IndexOf(spans, TokenKind.Identifier, 2);
        Assert.Equal(-1, spans.Start(beyond));
        Assert.Equal(-1, spans.End(beyond));
    }

    [Fact]
    public void End_NewlineAndEndOfFile_HaveNoWidth() {
        TokenSpans spans = Spans("x := 1\n");
        int newline = IndexOf(spans, TokenKind.Newline);

        Assert.Equal(spans.Start(newline), spans.End(newline));
    }

    [Fact]
    public void IndexAt_TokenStart_GivesTheToken_AndAGapGivesMinusOne() {
        TokenSpans spans = Spans("x := 1\n");

        Assert.Equal(1, spans.IndexAt(2));
        Assert.Equal(-1, spans.IndexAt(1));
    }

    [Fact]
    public void IndexAt_Location_GivesTheTokenAtThatLineAndColumn() {
        TokenSpans spans = Spans("a := 1\nb := 2\n");

        Assert.Equal(spans.Find(0, TokenKind.Identifier) + 4, spans.IndexAt(new SourceLocation("t.grob", 2, 1)));
    }

    [Fact]
    public void OffsetOf_LocationPastTheText_IsMinusOne() {
        TokenSpans spans = Spans("x := 1\n");

        Assert.Equal(-1, spans.OffsetOf(new SourceLocation("t.grob", 9, 1)));
        Assert.Equal(-1, spans.OffsetOf(new SourceLocation("t.grob", 1, 99)));
    }

    [Theory]
    [InlineData("v := f(a, g(b, c)), 1\n", 2, "f(a, g(b, c))")]
    [InlineData("v := a + b\n", 2, "a + b")]
    [InlineData("xs.filter(x => x > 1)\n", 4, "x => x > 1")]
    [InlineData("v := [1, 2][0]\n", 2, "[1, 2][0]")]
    [InlineData("v := #{ a: 1, b: 2 }.a\n", 2, "#{ a: 1, b: 2 }.a")]
    [InlineData("w := (a + b)\n", 3, "a + b")]
    public void ExpressionLast_StopsAtACommaNewlineOrClosingBracketOfItsOwnNesting(string source, int first, string expected) {
        TokenSpans spans = Spans(source);

        int last = spans.ExpressionLast(first);

        Assert.Equal(expected, spans.Text(spans.Start(first), spans.End(last)));
    }

    [Fact]
    public void ExpressionLast_FirstTokenEndsIt_GivesTheTokenBefore() {
        // `f(1, 2)`: token 3 is the comma, which ends an expression at once.
        Assert.Equal(2, Spans("f(1, 2)\n").ExpressionLast(3));
    }

    [Theory]
    [InlineData("x: int := 1\n", 2, "int")]
    [InlineData("x: int? := 1\n", 2, "int?")]
    [InlineData("x: int[] := [1]\n", 2, "int[]")]
    [InlineData("m: map<string, int> := f()\n", 2, "map<string, int>")]
    [InlineData("fn f(a: int, b: string): int {\n}\n", 5, "int")]
    [InlineData("h: fn(int, int): bool := g\n", 2, "fn(int, int): bool")]
    public void TypeLast_RunsThroughTheTypeAndNoFurther(string source, int first, string expected) {
        TokenSpans spans = Spans(source);

        int last = spans.TypeLast(first);

        Assert.Equal(expected, spans.Text(spans.Start(first), spans.End(last)));
    }

    [Fact]
    public void MatchingClose_NestedBrackets_FindsTheCloserOfTheSameDepth() {
        TokenSpans spans = Spans("f(a, [g(b)])\n");
        int open = IndexOf(spans, TokenKind.LeftParen);

        Assert.Equal(IndexOf(spans, TokenKind.RightParen, 1), spans.MatchingClose(open));
    }

    [Fact]
    public void MatchingClose_Unclosed_IsMinusOne() {
        TokenSpans spans = Spans("f(a\n");

        Assert.Equal(-1, spans.MatchingClose(IndexOf(spans, TokenKind.LeftParen)));
    }

    [Fact]
    public void Find_MissingKind_IsMinusOne() {
        Assert.Equal(-1, Spans("x := 1\n").Find(0, TokenKind.Arrow));
    }
}
