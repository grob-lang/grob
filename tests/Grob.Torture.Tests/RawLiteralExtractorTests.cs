using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The multi-line C# raw string literals in the test projects are seeds (gate report §1.3). The
/// extractor applies C#'s own rule: the closing quotes' indentation is removed from every line.
/// </summary>
public sealed class RawLiteralExtractorTests {
    private const string Q = "\"\"\"";
    private const string Q4 = "\"\"\"\"";

    [Fact]
    public void Extract_IndentedLiteral_StripsTheClosersIndentation() {
        string cs = $"        const string s = {Q}\n            x := 1\n              print(x)\n            {Q};\n";

        Assert.Equal(["x := 1\n  print(x)"], RawLiteralExtractor.Extract(cs));
    }

    // A one-line raw literal may end a line, its statement going on below; its closing quotes are
    // then at the end of the line and must not read as an opener.
    [Fact]
    public void Extract_OneLineLiteralClosingAtTheEndOfALine_IsNotAnOpener() {
        string cs = $"Console.WriteLine({Q}ignored{Q}\n);\nconst string source = {Q}\n    print(1)\n    {Q};\n";

        Assert.Equal(["print(1)"], RawLiteralExtractor.Extract(cs));
    }

    // An opener after a one-line literal on the same line, as AssertEmits("""x""", """ writes it,
    // is still an opener: an even number of quote runs precedes it.
    [Fact]
    public void Extract_OpenerAfterAOneLineLiteralOnItsLine_IsStillAnOpener() {
        string cs = $"AssertEmits({Q}x := 1{Q}, {Q}\n    Constant 1\n    {Q});\n";

        Assert.Equal(["Constant 1"], RawLiteralExtractor.Extract(cs));
    }

    // A four-quote literal may hold a run of three quotes, which is its content and neither
    // closes it nor opens another, so the runs are matched by width, not counted.
    [Fact]
    public void Extract_FourQuoteOneLineLiteralHoldingThreeQuotesAndClosingTheLine_IsNotAnOpener() {
        string cs = $"Console.WriteLine({Q4}a{Q}b{Q4}\n);\nconst string source = {Q4}\n    print(1)\n    {Q4};\n";

        Assert.Equal(["print(1)"], RawLiteralExtractor.Extract(cs));
    }

    [Fact]
    public void Extract_OpenerAfterAFourQuoteLiteralHoldingThreeQuotes_IsStillAnOpener() {
        string cs = $"AssertEmits({Q4}a {Q} b{Q4}, {Q}\n    print(1)\n    {Q});\n";

        Assert.Equal(["print(1)"], RawLiteralExtractor.Extract(cs));
    }

    [Fact]
    public void Extract_InterpolatedLiteral_IsSkipped() {
        string cs = $"var s = ${Q}\n    x := {{n}}\n    {Q};\nvar t = $${Q}\n    y\n    {Q};\n";

        Assert.Empty(RawLiteralExtractor.Extract(cs));
    }

    [Fact]
    public void Extract_FourQuoteLiteral_ClosesOnlyOnFourQuotes() {
        string cs = "var s = \"\"\"\"\n    a := \"\"\"inner\"\"\"\n    \"\"\"\";\n";

        Assert.Equal(["a := \"\"\"inner\"\"\""], RawLiteralExtractor.Extract(cs));
    }

    [Fact]
    public void Extract_SingleLineLiteral_IsSkipped() {
        Assert.Empty(RawLiteralExtractor.Extract($"var s = {Q}x := 1{Q};\n"));
    }

    [Fact]
    public void Extract_UnterminatedLiteral_IsSkipped() {
        Assert.Empty(RawLiteralExtractor.Extract($"var s = {Q}\n    x := 1\n"));
    }

    [Fact]
    public void Extract_TwoLiteralsAndBlankLines_KeepsBlankLinesEmpty() {
        string cs = $"a({Q}\n    x := 1\n\n    y := 2\n    {Q});\nb({Q}\n  z\n  {Q});\n";

        Assert.Equal(["x := 1\n\ny := 2", "z"], RawLiteralExtractor.Extract(cs));
    }

    [Fact]
    public void Extract_QuotesInALineComment_AreNotAnOpener() {
        string cs = $"// ends with {Q}\nconst string source = {Q}\n    print(1)\n    {Q};\n";

        Assert.Equal(["print(1)"], RawLiteralExtractor.Extract(cs));
    }

    [Fact]
    public void Extract_CarriageReturnLineEndings_AreNormalised() {
        string cs = $"var s = {Q}\r\n    x := 1\r\n    {Q};\r\n";

        Assert.Equal(["x := 1"], RawLiteralExtractor.Extract(cs));
    }
}
