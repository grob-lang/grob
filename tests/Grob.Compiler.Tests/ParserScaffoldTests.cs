using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

public class ParserScaffoldTests {
    [Fact]
    public void EmptySource_ProducesEmptyCompilationUnit() {
        CompilationUnit unit = ParseOk("");
        Assert.Empty(unit.TopLevel);
    }

    [Fact]
    public void WhitespaceAndNewlinesOnly_ProducesEmptyCompilationUnit() {
        CompilationUnit unit = ParseOk("\n\n\n");
        Assert.Empty(unit.TopLevel);
    }

    [Fact]
    public void RejectsTokensWithoutEofTerminator() {
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            Parser.Parse([], new DiagnosticBag()));
        Assert.Contains("EOF", ex.Message);
    }

    [Fact]
    public void ParsesTopLevelExpressionStatement() {
        CompilationUnit unit = ParseOk("42\n");
        ExpressionStmt s = Assert.IsType<ExpressionStmt>(unit.TopLevel[0]);
        IntLiteralExpr lit = Assert.IsType<IntLiteralExpr>(s.Expression);
        Assert.Equal(42L, lit.Value);
    }

    [Fact]
    public void MultipleTopLevelStatementsSeparatedByNewlines() {
        CompilationUnit unit = ParseOk("1\n2\n3\n");
        Assert.Equal(3, unit.TopLevel.Count);
        Assert.All(unit.TopLevel, n => Assert.IsType<ExpressionStmt>(n));
    }
}
