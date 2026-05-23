namespace Grob.Core.Tests;

using Grob.Core;
using Xunit;

/// <summary>
/// Verifies that every token kind required by grob-v1-requirements.md §3.4 is present
/// in the <see cref="TokenKind"/> enum.
/// </summary>
public sealed class TokenKindTests {
    // -------------------------------------------------------------------------
    // Enum surface — keywords (§3.4)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(TokenKind.Fn)]
    [InlineData(TokenKind.If)]
    [InlineData(TokenKind.Else)]
    [InlineData(TokenKind.While)]
    [InlineData(TokenKind.For)]
    [InlineData(TokenKind.In)]
    [InlineData(TokenKind.Return)]
    [InlineData(TokenKind.Const)]
    [InlineData(TokenKind.Readonly)]
    [InlineData(TokenKind.Type)]
    [InlineData(TokenKind.Param)]
    [InlineData(TokenKind.Import)]
    [InlineData(TokenKind.As)]
    [InlineData(TokenKind.Try)]
    [InlineData(TokenKind.Catch)]
    [InlineData(TokenKind.Finally)]
    [InlineData(TokenKind.Throw)]
    [InlineData(TokenKind.Select)]
    [InlineData(TokenKind.Case)]
    [InlineData(TokenKind.Default)]
    [InlineData(TokenKind.Break)]
    [InlineData(TokenKind.Continue)]
    [InlineData(TokenKind.True)]
    [InlineData(TokenKind.False)]
    [InlineData(TokenKind.Nil)]
    [InlineData(TokenKind.Step)]
    [InlineData(TokenKind.Switch)]
    public void Keywords_AllRequiredKindsExist(TokenKind kind) {
        Assert.True(Enum.IsDefined(kind));
    }

    // -------------------------------------------------------------------------
    // Enum surface — operators (§3.4)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(TokenKind.Plus)]
    [InlineData(TokenKind.Minus)]
    [InlineData(TokenKind.Star)]
    [InlineData(TokenKind.Slash)]
    [InlineData(TokenKind.Percent)]
    [InlineData(TokenKind.Assign)]
    [InlineData(TokenKind.ColonAssign)]
    [InlineData(TokenKind.EqualEqual)]
    [InlineData(TokenKind.BangEqual)]
    [InlineData(TokenKind.Less)]
    [InlineData(TokenKind.Greater)]
    [InlineData(TokenKind.LessEqual)]
    [InlineData(TokenKind.GreaterEqual)]
    [InlineData(TokenKind.Bang)]
    [InlineData(TokenKind.AmpAmp)]
    [InlineData(TokenKind.PipePipe)]
    [InlineData(TokenKind.Question)]
    [InlineData(TokenKind.Colon)]
    [InlineData(TokenKind.QuestionQuestion)]
    [InlineData(TokenKind.QuestionDot)]
    [InlineData(TokenKind.PlusAssign)]
    [InlineData(TokenKind.MinusAssign)]
    [InlineData(TokenKind.StarAssign)]
    [InlineData(TokenKind.SlashAssign)]
    [InlineData(TokenKind.PercentAssign)]
    [InlineData(TokenKind.PlusPlus)]
    [InlineData(TokenKind.MinusMinus)]
    [InlineData(TokenKind.DotDot)]
    [InlineData(TokenKind.Arrow)]
    public void Operators_AllRequiredKindsExist(TokenKind kind) {
        Assert.True(Enum.IsDefined(kind));
    }

    // -------------------------------------------------------------------------
    // Enum surface — punctuation (§3.4)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(TokenKind.LeftParen)]
    [InlineData(TokenKind.RightParen)]
    [InlineData(TokenKind.LeftBrace)]
    [InlineData(TokenKind.RightBrace)]
    [InlineData(TokenKind.LeftBracket)]
    [InlineData(TokenKind.RightBracket)]
    [InlineData(TokenKind.Comma)]
    [InlineData(TokenKind.Dot)]
    [InlineData(TokenKind.HashBrace)]
    [InlineData(TokenKind.DocComment)]
    public void Punctuation_AllRequiredKindsExist(TokenKind kind) {
        Assert.True(Enum.IsDefined(kind));
    }

    // -------------------------------------------------------------------------
    // Enum surface — decorators (§3.4)
    // -------------------------------------------------------------------------

    [Fact]
    public void Decorators_AtExists() {
        Assert.True(Enum.IsDefined(TokenKind.At));
    }

    // -------------------------------------------------------------------------
    // Enum surface — literals (§3.4)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(TokenKind.IntLiteral)]
    [InlineData(TokenKind.FloatLiteral)]
    [InlineData(TokenKind.StringStart)]
    [InlineData(TokenKind.StringPart)]
    [InlineData(TokenKind.StringEnd)]
    [InlineData(TokenKind.InterpStart)]
    [InlineData(TokenKind.InterpEnd)]
    [InlineData(TokenKind.RawStringLiteral)]
    [InlineData(TokenKind.RawStringBlockLiteral)]
    [InlineData(TokenKind.RegexLiteral)]
    [InlineData(TokenKind.Identifier)]
    public void Literals_AllRequiredKindsExist(TokenKind kind) {
        Assert.True(Enum.IsDefined(kind));
    }

    // -------------------------------------------------------------------------
    // Enum surface — structure (§3.4)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(TokenKind.Newline)]
    [InlineData(TokenKind.Eof)]
    [InlineData(TokenKind.Error)]
    public void Structure_AllRequiredKindsExist(TokenKind kind) {
        Assert.True(Enum.IsDefined(kind));
    }

    // -------------------------------------------------------------------------
    // Built-ins are NOT keywords (§3.4 note)
    // -------------------------------------------------------------------------

    [Fact]
    public void BuiltInNames_PrintIsNotAKeyword() {
        // print, exit, input are identifiers resolved at type-check time — no
        // dedicated token kind should exist for them.
        var names = Enum.GetNames<TokenKind>();
        Assert.DoesNotContain("Print", names, StringComparer.Ordinal);
        Assert.DoesNotContain("Exit", names, StringComparer.Ordinal);
        Assert.DoesNotContain("Input", names, StringComparer.Ordinal);
    }

    [Fact]
    public void EnumSurface_HasExpectedCardinality() {
        Assert.Equal(81, Enum.GetValues<TokenKind>().Length);
    }
}
