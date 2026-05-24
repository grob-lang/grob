using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerSnippetTests {
    [Fact]
    public void Variable_declaration_with_int_literal() {
        IReadOnlyList<Token> tokens = Lex("x := 42");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.IntLiteral, TokenKind.Eof);
        Assert.Equal("x", tokens[0].Lexeme);
        Assert.Equal("42", tokens[2].Lexeme);
    }

    [Fact]
    public void Function_declaration_returning_int() {
        IReadOnlyList<Token> tokens = Lex(
            "fn add(a: int, b: int): int {\n    return a + b\n}\n");
        AssertKinds(tokens,
            TokenKind.Fn, TokenKind.Identifier, TokenKind.LeftParen,
            TokenKind.Identifier, TokenKind.Colon, TokenKind.Identifier, TokenKind.Comma,
            TokenKind.Identifier, TokenKind.Colon, TokenKind.Identifier,
            TokenKind.RightParen, TokenKind.Colon, TokenKind.Identifier,
            TokenKind.LeftBrace,
            TokenKind.Return, TokenKind.Identifier, TokenKind.Plus, TokenKind.Identifier, TokenKind.Newline,
            TokenKind.RightBrace, TokenKind.Newline,
            TokenKind.Eof);
    }

    [Fact]
    public void Print_with_interpolation() {
        IReadOnlyList<Token> tokens = Lex("print(\"hi ${name}\")");
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.LeftParen,
            TokenKind.StringStart,
            TokenKind.StringPart,
            TokenKind.InterpStart, TokenKind.Identifier, TokenKind.InterpEnd,
            TokenKind.StringEnd,
            TokenKind.RightParen,
            TokenKind.Eof);
    }

    [Fact]
    public void Method_chain_with_leading_dot() {
        IReadOnlyList<Token> tokens = Lex(
            "result := files\n    .filter(f => f.ext == `.log`)\n    .sort()");
        // No Newline should survive between the chain segments — leading dots
        // suppress them.
        Assert.DoesNotContain(tokens, t => t.Kind == TokenKind.Newline);
        AssertKinds(tokens,
            TokenKind.Identifier, TokenKind.ColonAssign, TokenKind.Identifier,
            TokenKind.Dot, TokenKind.Identifier, TokenKind.LeftParen,
            TokenKind.Identifier, TokenKind.Arrow,
            TokenKind.Identifier, TokenKind.Dot, TokenKind.Identifier,
            TokenKind.EqualEqual, TokenKind.RawStringLiteral,
            TokenKind.RightParen,
            TokenKind.Dot, TokenKind.Identifier, TokenKind.LeftParen, TokenKind.RightParen,
            TokenKind.Eof);
    }
}
