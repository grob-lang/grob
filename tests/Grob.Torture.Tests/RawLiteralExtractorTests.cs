using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The multi-line C# raw string literals in the test projects are seeds (gate report §1.3). The
/// extractor applies C#'s own rule: the closing quotes' indentation is removed from every line.
/// </summary>
public sealed class RawLiteralExtractorTests {
    private const string Q = "\"\"\"";

    [Fact]
    public void Extract_IndentedLiteral_StripsTheClosersIndentation() {
        string cs = $"        const string s = {Q}\n            x := 1\n              print(x)\n            {Q};\n";

        Assert.Equal(["x := 1\n  print(x)"], RawLiteralExtractor.Extract(cs));
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
    public void Extract_CarriageReturnLineEndings_AreNormalised() {
        string cs = $"var s = {Q}\r\n    x := 1\r\n    {Q};\r\n";

        Assert.Equal(["x := 1"], RawLiteralExtractor.Extract(cs));
    }
}
