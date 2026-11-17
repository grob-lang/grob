using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// The type checker's half of the <c>regex</c> increment (D-432, D-440): a regex literal
/// is a <c>Regex</c> value whose members resolve, and E0602 reports an invalid pattern in
/// a literal, in a string-literal pattern argument to a <c>regex</c> function and in
/// <c>@pattern</c>'s argument. The caret sits where .NET stopped parsing when the
/// literal's text maps one-to-one onto the pattern; otherwise at the opening delimiter.
/// </summary>
public sealed class TypeCheckerRegexTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        return bag;
    }

    private static string FormatErrors(DiagnosticBag bag) =>
        string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"));

    private static void AssertSingle(DiagnosticBag bag, string code, int line, int column) {
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(code, diag.Code);
        Assert.Equal(line, diag.Range.Start.Line);
        Assert.Equal(column, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // The literal is a Regex
    // -----------------------------------------------------------------------

    [Fact]
    public void Literal_BindsAsARegexWithTypedMembers() {
        DiagnosticBag bag = Check("""
            r := /\d+/i
            n: int := r.pattern
            """);

        AssertSingle(bag, "E0001", 2, 11);
        Assert.Contains("type 'string'", bag.Errors.Single().Message);
    }

    [Fact]
    public void Literal_MethodCallsResolveDirectlyAndThroughABinding() {
        DiagnosticBag bag = Check("""
            ok: bool := /\d+/.isMatch("1")
            r := /(?<n>\d+)/
            first: Match? := r.match("a1")
            all: Match[] := r.matchAll("1 2")
            parts: string[] := /,/.split("a,b")
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Literal_UnknownMember_ReportsE1002() {
        DiagnosticBag bag = Check("""
            r := /a/
            r.test("a")
            """);

        AssertSingle(bag, "E1002", 2, 1);
        Assert.Contains("'Regex'", bag.Errors.Single().Message);
    }

    [Fact]
    public void Literal_PassesAsARegexParameter() {
        DiagnosticBag bag = Check("""
            fn hits(r: Regex, s: string): bool {
                return r.isMatch(s)
            }
            print(hits(/a/, "a"))
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Literal_IsNotAString() {
        DiagnosticBag bag = Check("""s: string := /a/""");

        AssertSingle(bag, "E0001", 1, 14);
    }

    [Fact]
    public void Literal_PropertyAssigned_ReportsE0204() {
        DiagnosticBag bag = Check("""
            r := /a/
            r.pattern = "b"
            """);

        AssertSingle(bag, "E0204", 2, 1);
    }

    // -----------------------------------------------------------------------
    // E0602 — a literal's body
    // -----------------------------------------------------------------------

    [Fact]
    public void Literal_InvalidPattern_ReportsE0602AtTheOffset() {
        // `/` at column 6; .NET stops at offset 2 of "a(", so the caret is column 9.
        DiagnosticBag bag = Check("x := /a(/");

        AssertSingle(bag, "E0602", 1, 9);
        Assert.Contains("insufficient closing parentheses", bag.Errors.Single().Message);
    }

    [Fact]
    public void Literal_InvalidPattern_DoesNotCascade() {
        DiagnosticBag bag = Check("""
            x := /a(/
            ok := x.isMatch("a")
            """);

        AssertSingle(bag, "E0602", 1, 9);
    }

    [Fact]
    public void Literal_EscapedSlashAndClassSlash_AreValid() {
        DiagnosticBag bag = Check("""
            a := /a\/b/
            b := /[^/]+$/
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // E0602 — string-literal pattern arguments
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("""ok := regex.isMatch("a(", "s")""")]
    [InlineData("""ok := regex.match("a(", "s")""")]
    [InlineData("""ok := regex.matchAll("a(", "s")""")]
    [InlineData("""ok := regex.split("a(", "s")""")]
    [InlineData("""ok := regex.replace("a(", "s", "t")""")]
    [InlineData("""ok := regex.replaceAll("a(", "s", "t")""")]
    [InlineData("""ok := regex.compile("a(")""")]
    public void PatternArgument_InvalidLiteral_ReportsE0602(string source) {
        DiagnosticBag bag = Check(source);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0602", diag.Code);
        int quote = source.IndexOf('"') + 1;
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(quote + 3, diag.Range.Start.Column);
    }

    [Fact]
    public void PatternArgument_Backtick_CaretAtTheOffset() {
        // The backtick is at column 20; .NET stops at offset 9 of "[unclosed".
        DiagnosticBag bag = Check("r := regex.compile(`[unclosed`)");

        AssertSingle(bag, "E0602", 1, 30);
        Assert.Contains("unterminated bracket", bag.Errors.Single().Message);
    }

    [Fact]
    public void PatternArgument_EscapeBeforeTheOffset_CaretAtTheQuote() {
        // The raw text `\\d+(` holds an escape before .NET's offset, so the caret
        // cannot be mapped exactly and sits at the opening quote, column 21.
        DiagnosticBag bag = Check("""ok := regex.isMatch("\\d+(", "s")""");

        AssertSingle(bag, "E0602", 1, 21);
    }

    [Fact]
    public void PatternArgument_IsValidatedAfterEscapes() {
        // Decoded, "\\(" is the pattern `\(`, an escaped parenthesis: valid. Read raw,
        // it would be a literal backslash and an unclosed group.
        DiagnosticBag bag = Check("""ok := regex.isMatch("\\(", "s")""");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Theory]
    [InlineData("""ok := regex.isMatch("a", "a(")""")]
    [InlineData("""ok := regex.escape("a(")""")]
    [InlineData("""ok := regex.replace("a", "s", "a(")""")]
    [InlineData("""ok := regex.compile("a", "zz")""")]
    public void NonPatternArgument_IsNotValidated(string source) =>
        Assert.False(Check(source).HasErrors);

    [Fact]
    public void PatternArgument_NotALiteral_IsLeftToRunTime() {
        DiagnosticBag bag = Check("""
            p := "a("
            ok := regex.isMatch(p, "s")
            q := "x"
            ok2 := regex.isMatch("${q}(", "s")
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // E0602 — @pattern's argument
    // -----------------------------------------------------------------------

    [Fact]
    public void PatternDecorator_InvalidRegex_ReportsE0602() {
        // The quote is at column 10; .NET stops at offset 9 of "[unclosed".
        DiagnosticBag bag = Check("@pattern(\"[unclosed\")\nparam tag: string\n");

        AssertSingle(bag, "E0602", 1, 20);
    }

    [Fact]
    public void PatternDecorator_ValidRegex_IsClean() =>
        Assert.False(Check("@pattern(`^[a-z]+(-[a-z]+)*$`)\nparam slug: string\n").HasErrors);
}
