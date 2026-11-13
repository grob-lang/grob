using Xunit;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The static layer's checks (D-428): each one has a failing inline example, built by breaking
/// one thing in a pair that otherwise passes. None of these checks is quarantinable.
/// </summary>
public sealed class ErrorExampleStaticCheckTests {
    private const string Slug = "p";

    private const string Source = "// E0001 adding a bool to an int\nx := 1 + true\n";

    private const string Master =
        "error[E0001]: type mismatch\n" +
        "  --> p.grob:2:6\n" +
        "   |\n" +
        " 2 | x := 1 + true\n" +
        "   |      ^^^^^^^^\n";

    private static readonly Dictionary<string, string> Titles = new(StringComparer.Ordinal) {
        ["E0001"] = "type mismatch",
        ["E0002"] = "incompatible operands",
    };

    private static IReadOnlyList<string> Check(string source = Source, string master = Master) =>
        ErrorExampleStaticChecks.CheckPair(Slug, source, master, Titles);

    [Fact]
    public void CheckPair_WellFormedPair_HasNoProblems() {
        Assert.Empty(Check());
    }

    [Fact]
    public void CheckPair_CrLfPair_HasNoProblems() {
        Assert.Empty(Check(Source.Replace("\n", "\r\n"), Master.Replace("\n", "\r\n")));
    }

    [Fact]
    public void CheckPair_MalformedHeader_Fails() {
        Assert.Equal(
            ["master line 1: malformed header 'error[E01]: type mismatch'", "master: no header"],
            Check(master: "error[E01]: type mismatch\n"));
    }

    [Fact]
    public void CheckPair_NoHeader_Fails() {
        string problem = Assert.Single(Check(master: "   = note: nothing\n"));
        Assert.Equal("master: no header", problem);
    }

    [Fact]
    public void CheckPair_TitleNotCatalogues_Fails() {
        string problem = Assert.Single(Check(master: Master.Replace("type mismatch", "mismatched types")));
        Assert.Equal("E0001: title 'mismatched types' is not the catalogue's 'type mismatch'", problem);
    }

    [Fact]
    public void CheckPair_CodeNotInCatalogue_Fails() {
        string problem = Assert.Single(Check(
            source: Source.Replace("E0001", "E0999"), master: Master.Replace("E0001", "E0999")));
        Assert.Equal("E0999: not in ErrorCatalog", problem);
    }

    [Fact]
    public void CheckPair_SourceFirstLineNotCodeComment_Fails() {
        string problem = Assert.Single(Check(source: "// adding a bool to an int\nx := 1 + true\n"));
        Assert.Equal("source: first line is not a // Exxxx comment", problem);
    }

    [Fact]
    public void CheckPair_SourceCommentNamesOtherCode_Fails() {
        string problem = Assert.Single(Check(source: Source.Replace("E0001", "E0002")));
        Assert.Equal("source: header comment names E0002, the master's first header E0001", problem);
    }

    [Fact]
    public void CheckPair_NoLocation_Fails() {
        Assert.Contains("E0001: no location", Check(master: Master.Replace("  --> p.grob:2:6\n", "")));
    }

    [Fact]
    public void CheckPair_LocationNamesOtherFile_Fails() {
        string problem = Assert.Single(Check(master: Master.Replace("p.grob", "q.grob")));
        Assert.Equal("E0001: location file 'q.grob' is not 'p.grob'", problem);
    }

    [Fact]
    public void CheckPair_LocationWithoutColumn_Fails() {
        string problem = Assert.Single(Check(master: Master.Replace("p.grob:2:6", "p.grob:2")));
        Assert.Equal("E0001: location 2 has no column", problem);
    }

    [Fact]
    public void CheckPair_SnippetLineDiffersFromSource_Fails() {
        string problem = Assert.Single(Check(master: Master.Replace(" 2 | x := 1 + true", " 2 | x := 1 + tru")));
        Assert.Equal("E0001: snippet line 2 'x := 1 + tru' is not source line 2 'x := 1 + true'", problem);
    }

    [Fact]
    public void CheckPair_SnippetLinePastEndOfSource_Fails() {
        string master = Master.Replace("p.grob:2:6", "p.grob:9:6").Replace(" 2 |", " 9 |");
        Assert.Contains("E0001: snippet line 9 'x := 1 + true' is not a line of the source", Check(master: master));
    }

    [Fact]
    public void CheckPair_NoCaret_Fails() {
        string problem = Assert.Single(Check(master: Master.Replace("   |      ^^^^^^^^\n", "")));
        Assert.Equal("E0001: no caret", problem);
    }

    [Fact]
    public void CheckPair_CaretOffLocationColumn_Fails() {
        string problem = Assert.Single(Check(master: Master.Replace("p.grob:2:6", "p.grob:2:10")));
        Assert.Equal("E0001: caret at column 6, location says 10", problem);
    }

    [Fact]
    public void CheckPair_CaretUnderOtherLine_Fails() {
        string problem = Assert.Single(Check(
            source: "// E0001 adding a bool to an int\nx := 1 + true\nx := 1 + true\n",
            master: Master.Replace(" 2 |", " 3 |")));
        Assert.Equal("E0001: first caret is under line 3, location says line 2", problem);
    }

    [Fact]
    public void CheckPair_CaretOnWhitespace_Fails() {
        string master = Master.Replace("p.grob:2:6", "p.grob:2:5").Replace("   |      ^", "   |     ^");
        string problem = Assert.Single(Check(master: master));
        Assert.Equal("E0001: caret column 5 is on whitespace or past the end of line 2", problem);
    }

    [Fact]
    public void CheckPair_CaretPastEndOfLine_Fails() {
        string master = Master.Replace("p.grob:2:6", "p.grob:2:20").Replace("   |      ^", "   |" + new string(' ', 20) + "^");
        string problem = Assert.Single(Check(master: master));
        Assert.Equal("E0001: caret column 20 is on whitespace or past the end of line 2", problem);
    }

    [Fact]
    public void CheckPair_NoTrailingNewline_Fails() {
        string problem = Assert.Single(Check(master: Master[..^1]));
        Assert.Equal("master: not exactly one trailing newline", problem);
    }

    [Fact]
    public void CheckPair_TwoTrailingNewlines_Fails() {
        string problem = Assert.Single(Check(master: Master + "\n"));
        Assert.Equal("master: not exactly one trailing newline", problem);
    }

    [Fact]
    public void CheckPair_AnsiEscape_Fails() {
        string problem = Assert.Single(Check(master: Master.Replace("type mismatch\n", "type mismatch\n\u001b[0m\n")));
        Assert.Equal("master: ANSI escape", problem);
    }

    [Theory]
    [InlineData("✅", "U+2705")]
    [InlineData("\U0001F680", "U+1F680")]
    public void CheckPair_Pictograph_Fails(string pictograph, string codePoint) {
        string problem = Assert.Single(Check(master: Master + $"   = note: done {pictograph}\n"));
        Assert.Equal($"master: pictograph {codePoint}", problem);
    }

    [Fact]
    public void CheckPair_ArrowIsTypographyNotPictograph_Passes() {
        Assert.Empty(Check(master: Master + "   = note: Parent → Child\n"));
    }

    [Theory]
    [InlineData("simply")]
    [InlineData("obviously")]
    [InlineData("merely")]
    [InlineData("of course")]
    [InlineData("Simply")]
    public void CheckPair_HedgeWord_Fails(string word) {
        string problem = Assert.Single(Check(master: Master + $"   = help: {word} remove it\n"));
        Assert.Equal($"master: hedge word '{word}'", problem);
    }

    [Fact]
    public void CheckPair_HedgeWordInsideAnotherWord_Passes() {
        Assert.Empty(Check(master: Master + "   = note: simplyfied is not a word\n"));
    }

    [Fact]
    public void CheckPair_SecondDiagnosticChecked_FailsOnIt() {
        string master = Master +
            "error[E0002]: incompatible operands\n" +
            "  --> p.grob:2:1\n" +
            " 2 | x := 1 + true\n" +
            "   |   ^\n";
        string problem = Assert.Single(Check(master: master));
        Assert.Equal("E0002: caret at column 3, location says 1", problem);
    }
}
