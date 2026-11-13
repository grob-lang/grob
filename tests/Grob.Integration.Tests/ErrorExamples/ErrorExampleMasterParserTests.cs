using Xunit;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The gold-master parser (D-428): every line form a master uses, read from inline strings so
/// each form is pinned independently of the live corpus.
/// </summary>
public sealed class ErrorExampleMasterParserTests {
    [Fact]
    public void Parse_ErrorHeader_ReadsSeverityCodeAndTitle() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse("error[E0001]: type mismatch\n");

        MasterDiagnostic diagnostic = Assert.Single(master.Diagnostics);
        Assert.Equal("error", diagnostic.Severity);
        Assert.Equal("E0001", diagnostic.Code);
        Assert.Equal("type mismatch", diagnostic.Title);
        Assert.Empty(master.FormatProblems);
    }

    [Fact]
    public void Parse_WarningHeader_ReadsWarningSeverity() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse("warning[E1101]: shadowed declaration\n");

        MasterDiagnostic diagnostic = Assert.Single(master.Diagnostics);
        Assert.Equal("warning", diagnostic.Severity);
        Assert.Equal("E1101", diagnostic.Code);
    }

    [Fact]
    public void Parse_MalformedHeader_RecordsFormatProblem() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse("error[E01]: too short\n");

        Assert.Empty(master.Diagnostics);
        string problem = Assert.Single(master.FormatProblems);
        Assert.Equal("line 1: malformed header 'error[E01]: too short'", problem);
    }

    [Fact]
    public void Parse_LocationWithColumn_ReadsFileLineAndColumn() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse("error[E0001]: type mismatch\n  --> x.grob:5:14\n");

        MasterLocation location = Assert.Single(master.Diagnostics).Location!;
        Assert.Equal("x.grob", location.File);
        Assert.Equal(5, location.Line);
        Assert.Equal(14, location.Column);
    }

    [Fact]
    public void Parse_LocationWithoutColumn_HasNullColumn() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse("error[E5902]: circular initialisation\n  --> x.grob:5\n");

        MasterLocation location = Assert.Single(master.Diagnostics).Location!;
        Assert.Equal(5, location.Line);
        Assert.Null(location.Column);
    }

    [Fact]
    public void Parse_MalformedLocation_RecordsFormatProblem() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse("error[E0001]: type mismatch\n  --> x.grob\n");

        Assert.Null(Assert.Single(master.Diagnostics).Location);
        string problem = Assert.Single(master.FormatProblems);
        Assert.Equal("line 2: malformed location '  --> x.grob'", problem);
    }

    [Fact]
    public void Parse_SecondLocationUnderOneHeader_KeepsFirst() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse(
            "error[E0001]: type mismatch\n  --> x.grob:5:14\n  --> x.grob:9:2\n");

        MasterLocation location = Assert.Single(master.Diagnostics).Location!;
        Assert.Equal(5, location.Line);
        Assert.Equal(14, location.Column);
    }

    [Fact]
    public void Parse_HeaderWithNoLocation_HasNullLocation() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse("error[E0001]: type mismatch\n   = note: no location\n");

        Assert.Null(Assert.Single(master.Diagnostics).Location);
        Assert.Empty(master.FormatProblems);
    }

    [Fact]
    public void Parse_SnippetLine_ReadsNumberAndText() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse(
            "error[E0001]: type mismatch\n  --> x.grob:3:1\n   |\n 3 | x := 1 + true\n");

        MasterSnippetLine snippet = Assert.Single(Assert.Single(master.Diagnostics).Snippets);
        Assert.Equal(3, snippet.LineNumber);
        Assert.Equal("x := 1 + true", snippet.Text);
    }

    [Fact]
    public void Parse_SnippetNumberingEmptyLine_ReadsEmptyText() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse(
            "error[E2002]: unterminated string literal\n  --> x.grob:4:1\n 4 |\n");

        MasterSnippetLine snippet = Assert.Single(Assert.Single(master.Diagnostics).Snippets);
        Assert.Equal(4, snippet.LineNumber);
        Assert.Equal(string.Empty, snippet.Text);
    }

    [Fact]
    public void Parse_CaretLine_ComputesColumnAgainstSnippetAbove() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse(
            "error[E0001]: type mismatch\n  --> x.grob:3:6\n   |\n 3 | x := 1 + true\n   |      ^^^^^^^^\n");

        MasterCaret caret = Assert.Single(master.Diagnostics).FirstCaret!;
        Assert.Equal(3, caret.LineNumber);
        Assert.Equal(6, caret.Column);
    }

    [Fact]
    public void Parse_TwoCaretLines_KeepsFirst() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse(
            "error[E0001]: type mismatch\n  --> x.grob:3:1\n 3 | x := 1\n   | ^\n 4 | y := 2\n   |      ^\n");

        MasterCaret caret = Assert.Single(master.Diagnostics).FirstCaret!;
        Assert.Equal(3, caret.LineNumber);
        Assert.Equal(1, caret.Column);
    }

    [Fact]
    public void Parse_CaretWithNoSnippetAbove_RecordsFormatProblem() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse(
            "error[E0001]: type mismatch\n  --> x.grob:3:1\n   | ^\n");

        Assert.Null(Assert.Single(master.Diagnostics).FirstCaret);
        string problem = Assert.Single(master.FormatProblems);
        Assert.Equal("line 3: caret with no snippet line above it", problem);
    }

    [Fact]
    public void Parse_TwoDiagnostics_ReadsBothInOrder() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse(
            "error[E0101]: nil dereference\n  --> x.grob:4:14\n 4 | a\n   | ^\n" +
            "error[E0005]: return type mismatch\n  --> x.grob:5:12\n 5 | b\n   | ^\n");

        Assert.Collection(master.Diagnostics,
            first => {
                Assert.Equal("E0101", first.Code);
                Assert.Equal(4, first.Location!.Line);
                Assert.Equal(4, Assert.Single(first.Snippets).LineNumber);
            },
            second => {
                Assert.Equal("E0005", second.Code);
                Assert.Equal(5, second.Location!.Line);
                Assert.Equal(5, Assert.Single(second.Snippets).LineNumber);
            });
    }

    [Fact]
    public void Parse_CrLfLineEndings_ReadsAsLf() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse(
            "error[E0001]: type mismatch\r\n  --> x.grob:3:6\r\n 3 | x := 1 + true\r\n   |      ^\r\n");

        MasterDiagnostic diagnostic = Assert.Single(master.Diagnostics);
        Assert.Equal("type mismatch", diagnostic.Title);
        Assert.Equal(6, diagnostic.Location!.Column);
        Assert.Equal("x := 1 + true", Assert.Single(diagnostic.Snippets).Text);
        Assert.Equal(6, diagnostic.FirstCaret!.Column);
    }

    [Fact]
    public void Parse_NoteAndChainLines_AreIgnored() {
        ErrorExampleMaster master = ErrorExampleMaster.Parse(
            "error[E5301]: file not found\n  --> x.grob:3:10\n   |\n   = note: path\n   |\n" +
            "   from fs.readLines (x.grob:3:10)\n   at <top-level> (x.grob:3:10)\n");

        MasterDiagnostic diagnostic = Assert.Single(master.Diagnostics);
        Assert.Empty(diagnostic.Snippets);
        Assert.Null(diagnostic.FirstCaret);
        Assert.Empty(master.FormatProblems);
    }
}
