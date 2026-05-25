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
        Assert.Equal(Severity.Error, bag.Diagnostics[0].Severity);

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

        Assert.True(bag.Count >= 1);
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

        Assert.True(bag.Count >= 3,
            $"expected ≥3 diagnostics, got {bag.Count}:\n{FormatDiagnostics(bag)}");
        Assert.Contains(unit.TopLevel, n => n is FnDecl f && f.Name == "good");
    }

    [Fact]
    public void GarbageAtTopLevel_RecoversAndContinues() {
        const string src =
            "@@@ !!!\n" +
            "x := 1\n";

        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);

        Assert.True(bag.Count >= 1);
        Assert.Contains(unit.TopLevel, n => n is VarDeclStmt v && v.Name == "x");
    }

    [Fact]
    public void DegenerateInput_ParserDoesNotLoop() {
        // A pile of tokens that lex cleanly but never start a valid statement.
        // The test passes simply by not hanging.
        const string src = ", , ,\n. . .\n";

        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);

        Assert.True(bag.Count >= 1);
        Assert.NotNull(unit);
    }
}
