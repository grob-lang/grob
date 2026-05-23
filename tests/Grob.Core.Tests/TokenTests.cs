using Grob.Core;
using Xunit;

namespace Grob.Core.Tests;

public sealed class TokenTests {
    private static readonly SourceLocation _testLocation = new("src/test.grob", 3, 7);

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_SetsAllProperties() {
        var token = new Token(TokenKind.Identifier, "foo", _testLocation, 0);

        Assert.Equal(TokenKind.Identifier, token.Kind);
        Assert.Equal("foo", token.Lexeme);
        Assert.Equal(_testLocation, token.Location);
        Assert.Equal(0, token.BracketDepth);
    }

    [Fact]
    public void Constructor_NonZeroBracketDepth_IsPreserved() {
        var token = new Token(TokenKind.IntLiteral, "42", _testLocation, 2);

        Assert.Equal(2, token.BracketDepth);
    }

    // -------------------------------------------------------------------------
    // Equality (record semantics)
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_SameValues_AreEqual() {
        var a = new Token(TokenKind.If, "if", _testLocation, 0);
        var b = new Token(TokenKind.If, "if", _testLocation, 0);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentKind_AreNotEqual() {
        var a = new Token(TokenKind.If, "if", _testLocation, 0);
        var b = new Token(TokenKind.Else, "else", _testLocation, 0);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentLexeme_AreNotEqual() {
        var a = new Token(TokenKind.Identifier, "foo", _testLocation, 0);
        var b = new Token(TokenKind.Identifier, "bar", _testLocation, 0);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentLocation_AreNotEqual() {
        var locA = new SourceLocation("src/a.grob", 1, 1);
        var locB = new SourceLocation("src/a.grob", 1, 2);

        var a = new Token(TokenKind.Identifier, "x", locA, 0);
        var b = new Token(TokenKind.Identifier, "x", locB, 0);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentBracketDepth_AreNotEqual() {
        var a = new Token(TokenKind.Newline, "", _testLocation, 0);
        var b = new Token(TokenKind.Newline, "", _testLocation, 1);

        Assert.NotEqual(a, b);
    }

    // -------------------------------------------------------------------------
    // ToString shape
    // -------------------------------------------------------------------------

    [Fact]
    public void ToString_NonEmptyLexeme_ContainsKindLexemeLocationAndDepth() {
        var location = new SourceLocation("src/foo.grob", 5, 3);
        var token = new Token(TokenKind.Identifier, "myVar", location, 0);
        var str = token.ToString();

        Assert.Contains("Identifier", str);
        Assert.Contains("myVar", str);
        Assert.Contains(location.ToString(), str);
        Assert.Contains("depth=0", str);
    }

    [Fact]
    public void ToString_EmptyLexeme_ContainsEmptyPlaceholder() {
        var token = new Token(TokenKind.Eof, "", new SourceLocation("src/foo.grob", 10, 1), 0);
        var str = token.ToString();

        Assert.Contains("Eof", str);
        Assert.Contains("<empty>", str);
    }

    [Fact]
    public void ToString_NonZeroDepth_ReflectsDepth() {
        var token = new Token(TokenKind.Comma, ",", _testLocation, 3);
        var str = token.ToString();

        Assert.Contains("depth=3", str);
    }
}
