using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerCommentTests {
    [Fact]
    public void LineComment_IsDiscarded() {
        AssertKinds(Lex("x // a comment\n"),
            TokenKind.Identifier, TokenKind.Newline, TokenKind.Eof);
    }

    [Fact]
    public void BlockComment_IsDiscarded() {
        AssertKinds(Lex("x /* block */ y"),
            TokenKind.Identifier, TokenKind.Identifier, TokenKind.Eof);
    }

    [Fact]
    public void MultilineBlockComment_AdvancesLineNumbers() {
        IReadOnlyList<Token> tokens = Lex("x /* line one\nline two */ y");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal(1, tokens[0].Location.Line);
        Assert.Equal(TokenKind.Identifier, tokens[1].Kind);
        Assert.Equal(2, tokens[1].Location.Line);
    }

    [Fact]
    public void DocComment_IsRecognisedAndDiscarded() {
        AssertKinds(Lex("/// doc line\nx"),
            TokenKind.Newline, TokenKind.Identifier, TokenKind.Eof);
    }

    [Fact]
    public void UnterminatedBlockComment_ReportsDiagnostic() {
        var (_, diagnostics) = LexWithDiagnostics("/* never closes");
        Assert.NotEmpty(diagnostics.Errors);
    }
}
