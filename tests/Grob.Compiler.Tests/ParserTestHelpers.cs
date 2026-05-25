using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

internal static class ParserTestHelpers {
    public static (CompilationUnit Unit, DiagnosticBag Diagnostics) Parse(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        // Any lexer diagnostics make the test ambiguous — parser tests assume
        // clean tokens unless they explicitly want lexer noise.
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        return (unit, bag);
    }

    public static CompilationUnit ParseOk(string source) {
        (CompilationUnit unit, DiagnosticBag bag) = Parse(source);
        Assert.True(bag.Count == 0, FormatDiagnostics(bag));
        return unit;
    }

    public static string FormatDiagnostics(DiagnosticBag bag) =>
        bag.Count == 0
            ? "(no diagnostics)"
            : string.Join('\n', bag.Diagnostics.Select(d => d.ToString()));

    public static T Single<T>(CompilationUnit unit) where T : AstNode {
        Assert.Single(unit.TopLevel);
        return Assert.IsType<T>(unit.TopLevel[0]);
    }

    public static T OnlyStmt<T>(BlockStmt block) where T : Statement {
        Assert.Single(block.Statements);
        return Assert.IsType<T>(block.Statements[0]);
    }

    public static Expression ExprOf(CompilationUnit unit) {
        ExpressionStmt s = Single<ExpressionStmt>(unit);
        return s.Expression;
    }
}
