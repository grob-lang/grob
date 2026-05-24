using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.LexerTestHelpers;

namespace Grob.Compiler.Tests;

public class LexerErrorRecoveryTests {
    [Fact]
    public void StrayCharacter_EmitsDiagnosticAndContinues() {
        var (tokens, diagnostics) = LexWithDiagnostics("a $ b");
        Assert.Single(diagnostics.Errors);
        // Lexer continued: it produced the trailing identifier 'b'.
        Assert.Contains(tokens, t => t.Kind == TokenKind.Identifier && t.Lexeme == "b");
    }

    [Fact]
    public void MultipleErrors_AllReported() {
        var (_, diagnostics) = LexWithDiagnostics("a $ b @ c & d");
        // $ is a stray; & alone is invalid. Two errors at minimum.
        Assert.True(diagnostics.Errors.Count() >= 2,
            $"expected at least two errors but got {diagnostics.Errors.Count()}");
    }

    [Fact]
    public void Lexer_NeverThrowsOnChaosInput() {
        // Pile of malformed inputs. The lexer must produce tokens and diagnostics
        // without throwing.
        const string Chaos = "fn $ { } @ \"unterminated\nx := 0xZZ\n`raw\n";
        var (tokens, _) = LexWithDiagnostics(Chaos);
        Assert.Equal(TokenKind.Eof, tokens[^1].Kind);
    }

    [Fact]
    public void SourceLocations_TrackLineAndColumn() {
        IReadOnlyList<Token> tokens = Lex("a\n  b");
        Token a = tokens[0];
        Token b = tokens.First(t => t.Lexeme == "b");
        Assert.Equal(1, a.Location.Line);
        Assert.Equal(1, a.Location.Column);
        Assert.Equal(2, b.Location.Line);
        Assert.Equal(3, b.Location.Column);
    }
}
