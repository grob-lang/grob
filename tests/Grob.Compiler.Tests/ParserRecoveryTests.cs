using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

public class ParserRecoveryTests {
    /// <summary>
    /// The grob-language-fundamentals §29.6 worked example. After the
    /// type-checker arrives in Sprint 2 the second diagnostic
    /// (undefined identifier <c>nonexistent</c>) lands too. In Sprint 1
    /// only the parser-level error is reported, the rest of the file
    /// parses cleanly, and no cascade diagnostics fire.
    /// </summary>
    [Fact]
    public void Section29_6_WorkedExample_ProducesOneParserDiagnostic() {
        const string src =
            "fn add(a: Int, b: Int): Int {\n" +
            "    return a +\n" +
            "}\n" +
            "\n" +
            "x := add(1, 2)\n" +
            "y := nonexistent + 5\n";

        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);

        Assert.Equal(1, bag.Count);
        Diagnostic d = bag.Diagnostics[0];
        Assert.Equal("E2001", d.Code);
        Assert.Equal(Severity.Error, d.Severity);
        // §29 Fix: the diagnostic must point at the dangling '+' (line 2, col 14),
        // not at the '}' anchor where the cursor lands after line-continuation
        // suppresses the newline between '+' and '}'.
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(14, d.Range.Start.Column);

        Assert.Equal(3, unit.TopLevel.Count);

        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[0]);
        ReturnStmt ret = Assert.IsType<ReturnStmt>(fn.Body.Statements[0]);
        Assert.IsType<ErrorExpr>(ret.Value);

        VarDeclStmt x = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        Assert.Equal("x", x.Name);
        Assert.IsType<CallExpr>(x.Initializer);

        VarDeclStmt y = Assert.IsType<VarDeclStmt>(unit.TopLevel[2]);
        Assert.Equal("y", y.Name);
        Assert.IsType<BinaryExpr>(y.Initializer);
    }

    [Fact]
    public void MissingClosingBrace_RecoversAtNextTopLevelKeyword() {
        const string src =
            "fn broken(): Int {\n" +
            "    return 1\n" +
            "// no closing brace — and broken statement below\n" +
            "    @@@\n" +
            "fn good(): Int { return 2 }\n";

        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);

        Assert.Equal(2, bag.Count);
        Assert.All(bag.Diagnostics, d => {
            Assert.Equal("E2001", d.Code);
            Assert.Equal(Severity.Error, d.Severity);
        });
        Assert.Equal((4, 5), (bag.Diagnostics[0].Range.Start.Line, bag.Diagnostics[0].Range.Start.Column));
        Assert.Equal((5, 1), (bag.Diagnostics[1].Range.Start.Line, bag.Diagnostics[1].Range.Start.Column));
        Assert.Contains(unit.TopLevel, n => n is FnDecl f && f.Name == "good");
    }

    [Fact]
    public void MultipleIndependentFailures_ProduceMultipleDiagnostics() {
        // `fn` with no name fails immediately at the missing identifier, which is
        // unrelated to expression-operator line-continuation. Recovery then
        // anchors at the next top-level `fn` keyword on the following line.
        const string src =
            "fn\n" +
            "fn\n" +
            "fn\n" +
            "fn good(): Int { return 4 }\n";

        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);

        Assert.Equal(3, bag.Count);
        Assert.All(bag.Diagnostics, d => {
            Assert.Equal("E2001", d.Code);
            Assert.Equal(Severity.Error, d.Severity);
            Assert.Equal(3, d.Range.Start.Column);
        });
        Assert.Equal(new[] { 1, 2, 3 }, bag.Diagnostics.Select(d => d.Range.Start.Line).ToArray());
        Assert.Contains(unit.TopLevel, n => n is FnDecl f && f.Name == "good");
    }

    [Fact]
    public void GarbageAtTopLevel_RecoversAndContinues() {
        const string src =
            "@@@ !!!\n" +
            "x := 1\n";

        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);

        Assert.Equal(1, bag.Count);
        Diagnostic d = bag.Diagnostics[0];
        Assert.Equal("E2001", d.Code);
        Assert.Equal(Severity.Error, d.Severity);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
        Assert.Contains(unit.TopLevel, n => n is VarDeclStmt v && v.Name == "x");
    }

    [Fact]
    public void DegenerateInput_ParserDoesNotLoop() {
        // A pile of tokens that lex cleanly but never start a valid statement.
        // The test passes simply by not hanging.
        const string src = ", , ,\n. . .\n";

        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);

        Assert.Equal(1, bag.Count);
        Diagnostic d = bag.Diagnostics[0];
        Assert.Equal("E2001", d.Code);
        Assert.Equal(Severity.Error, d.Severity);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
        Assert.NotNull(unit);
    }

    // -----------------------------------------------------------------------
    // §29.2 error-node range contract tests
    // -----------------------------------------------------------------------

    /// <summary>
    /// §29.2: the ErrorExpr's range must not include the anchor token.
    /// In the worked example the anchor is <c>}</c> at line 3 col 1; the last
    /// consumed token before the anchor is <c>+</c> at line 2 col 14.
    /// So the ErrorExpr should span from <c>a</c> (col 12) to <c>+</c> (col 14).
    /// </summary>
    [Fact]
    public void Section29_2_ErrorExpr_RangeExcludesAnchor() {
        const string src =
            "fn add(a: Int, b: Int): Int {\n" +
            "    return a +\n" +
            "}\n";

        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);

        // Full diagnostic contract: code, position at the dangling '+'.
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(14, d.Range.Start.Column);

        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[0]);
        ReturnStmt ret = Assert.IsType<ReturnStmt>(fn.Body.Statements[0]);
        ErrorExpr err = Assert.IsType<ErrorExpr>(ret.Value);

        // Start: the expression started at 'a' (line 2, col 12).
        Assert.Equal(2, err.Range.Start.Line);
        Assert.Equal(12, err.Range.Start.Column);
        // End (inclusive): the last consumed token was '+' (line 2, col 14).
        // Must NOT be the '}' anchor at (3, 1).
        Assert.Equal(2, err.Range.End.Line);
        Assert.Equal(14, err.Range.End.Column);
    }

    /// <summary>
    /// §29.2 for ErrorDecl: a broken top-level declaration's range must also
    /// exclude the anchor. Using a bare unknown token at top level, the
    /// ErrorDecl spans only that token — anchor (newline) is not included.
    /// </summary>
    [Fact]
    public void Section29_2_ErrorDecl_RangeExcludesAnchor() {
        // '@@' is not a valid top-level item. Recovery anchors at the newline.
        // The ErrorDecl range should cover '@@' and stop before the newline.
        const string src = "@@\nx := 1\n";

        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);

        // Full diagnostic contract: code and position at the first '@'.
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);

        AstNode first = unit.TopLevel[0];
        ErrorDecl err = Assert.IsType<ErrorDecl>(first);
        // The error range starts at '@' and must not extend to the newline anchor.
        Assert.Equal(1, err.Range.Start.Line);
        // At least one token was consumed before anchoring — the range end must
        // remain on line 1, not spill into line 2.
        Assert.Equal(1, err.Range.End.Line);
    }

    /// <summary>
    /// When a binary operator ends a line and line-continuation is active,
    /// the diagnostic must point at the operator, not the token that follows it.
    /// (Covers all arithmetic and logical operators in one representative check.)
    /// </summary>
    [Theory]
    [InlineData("fn f(): Int {\n    return a *\n}\n", "*", 2, 14)]
    [InlineData("fn f(): Int {\n    return a -\n}\n", "-", 2, 14)]
    [InlineData("fn f(): Int {\n    return a &&\n}\n", "&&", 2, 14)]
    [InlineData("fn f(): Int {\n    return a ||\n}\n", "||", 2, 14)]
    [InlineData("fn f(): Int {\n    return a ==\n}\n", "==", 2, 14)]
    public void DanglingBinaryOperator_DiagnosticPointsAtOperator(
        string src, string opLexeme, int expectedLine, int expectedCol) {
        (_, DiagnosticBag bag) = Parse(src);
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(expectedLine, d.Range.Start.Line);
        Assert.Equal(expectedCol, d.Range.Start.Column);
        _ = opLexeme; // confirms the intent in the InlineData attribute
    }
}
