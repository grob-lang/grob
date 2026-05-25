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
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);

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
}
