using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// The completed <c>/</c> rule (D-432, fundamentals §8) as the parser sees it: the lexer's
/// division-or-regex decision must reach the parser as the right number of statements.
/// </summary>
public class SlashRuleParseTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) Parse(string source) {
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        return (unit, bag);
    }

    [Fact]
    public void DivisionBeginningALine_InsideParentheses_IsOneStatement() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("total := (a\n    / b)\n");

        Assert.Empty(bag.Diagnostics);
        Assert.Single(unit.TopLevel);
    }

    [Fact]
    public void DivisionBeginningALine_InsideSquareBrackets_IsOneStatement() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := xs[a\n    / 2]\n");

        Assert.Empty(bag.Diagnostics);
        Assert.Single(unit.TopLevel);
    }

    [Fact]
    public void RegexBeginningALine_AfterAStatement_IsASecondStatement() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := 1\n/abc/.isMatch(s)\n");

        Assert.Empty(bag.Diagnostics);
        Assert.Equal(2, unit.TopLevel.Count);
    }

    [Fact]
    public void CompoundDivision_AfterAnIdentifier_IsOneStatement() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("n := 20\nn /= 4\n");

        Assert.Empty(bag.Diagnostics);
        Assert.Equal(2, unit.TopLevel.Count);
    }

    [Fact]
    public void RegexWithSlashInsideCharacterClass_ParsesAsOneLiteral() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("lastSegment := /[^/]+$/\n");

        Assert.Empty(bag.Diagnostics);
        Assert.Single(unit.TopLevel);
    }
}
