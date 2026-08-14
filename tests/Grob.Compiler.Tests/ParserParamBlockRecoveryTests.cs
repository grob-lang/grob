using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser recovery tests for <c>param</c>-block entries (D-406, closing the finding
/// D-405 left open for this construct). A <c>param</c> block is a
/// <b>newline-separated</b>, brace-delimited element list — the same shape as
/// <c>type</c>-declaration fields (see <c>ParserTypeDeclRecoveryTests.cs</c>) — so
/// it shares the identical <c>SkipToNextLiteralElementBoundary</c> newline mode via
/// its own wrapper, <c>ParseDeclaredParameterOrError</c>.
/// <c>ParseParameterList</c> (function parameter lists, comma-separated inside
/// parens) is a different call site of the same underlying <c>ParseDeclaredParameter</c>
/// and is out of scope — not one of D-406's four constructs.
/// </summary>
public sealed class ParserParamBlockRecoveryTests {
    [Fact]
    public void MalformedEntry_MissingColon_RecoversWithOneDiagnostic() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("param {\nbad\ny: int\n}\nx := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ':' after parameter name", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(4, d.Range.Start.Column);

        ParamBlockDecl decl = Assert.IsType<ParamBlockDecl>(unit.TopLevel[0]);
        Parameter param = Assert.Single(decl.Parameters);
        Assert.Equal("y", param.Name);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    /// <summary>
    /// Load-bearing (D-405 shape): proves the fix removes the phantom duplicate
    /// without suppressing a second, genuinely independent mistake in the same
    /// block.
    /// </summary>
    [Fact]
    public void MalformedEntry_TwoDistinctMalformedEntries_ProducesTwoDiagnosticsNoneSwallowed() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("param {\nbad\nbad2\ny: int\n}\nx := 2\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic first = bag.Diagnostics[0];
        Assert.Equal("E2001", first.Code);
        Assert.Equal("expected ':' after parameter name", first.Message);
        Assert.Equal(2, first.Range.Start.Line);
        Assert.Equal(4, first.Range.Start.Column);

        Diagnostic second = bag.Diagnostics[1];
        Assert.Equal("E2001", second.Code);
        Assert.Equal("expected ':' after parameter name", second.Message);
        Assert.Equal(3, second.Range.Start.Line);
        Assert.Equal(5, second.Range.Start.Column);

        ParamBlockDecl decl = Assert.IsType<ParamBlockDecl>(unit.TopLevel[0]);
        Parameter param = Assert.Single(decl.Parameters);
        Assert.Equal("y", param.Name);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedEntry_SubsequentDeclaration_TypeChecksCleanly() {
        const string src = "param {\nbad\ny: int\n}\nfn good(): int { return 1 }\n";
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Diagnostic parseDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", parseDiagnostic.Code);

        new TypeChecker(bag).Check(unit);

        Diagnostic onlyDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Same(parseDiagnostic, onlyDiagnostic);

        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }

    // -----------------------------------------------------------------------
    // Error recovery — delimiters the abandoned entry opened before it failed
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedEntry_ValueClosesItsOwnBracketPair_RecoversAtNextEntryOnly() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("param {\nx: int = foo(1 2)\ny: int\n}\nfn good(): int { return 2 }\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(16, d.Range.Start.Column); // the stray '2'

        ParamBlockDecl decl = Assert.IsType<ParamBlockDecl>(unit.TopLevel[0]);
        Parameter param = Assert.Single(decl.Parameters);
        Assert.Equal("y", param.Name);

        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }

    [Fact]
    public void MalformedEntry_ValueLeavesBracketPairOpen_DoesNotFabricateNextEntry() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("param {\nx: int = foo(1\ny: int\n}\nfn good(): int { return 2 }\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);

        ParamBlockDecl decl = Assert.IsType<ParamBlockDecl>(unit.TopLevel[0]);
        Assert.Empty(decl.Parameters);

        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }

    [Fact]
    public void MalformedEntry_UnterminatedAfterMalformedEntry_ReportsBothRootCauses() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("param {\nbad\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic colon = bag.Diagnostics[0];
        Assert.Equal("E2001", colon.Code);
        Assert.Equal("expected ':' after parameter name", colon.Message);
        Assert.Equal(2, colon.Range.Start.Line);
        Assert.Equal(4, colon.Range.Start.Column);

        Diagnostic brace = bag.Diagnostics[1];
        Assert.Equal("E2001", brace.Code);
        Assert.Equal("expected '}' to close param block", brace.Message);
        Assert.Equal(3, brace.Range.Start.Line);
        Assert.Equal(1, brace.Range.Start.Column);

        Assert.NotNull(unit);
    }

    // -----------------------------------------------------------------------
    // Keyword anchor — the newline-boundary variant's own confirmed design point
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedEntry_UnterminatedBody_RecoversAtNextTopLevelKeyword() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("param {\nx: int = foo(1\nfn good(): int { return 2 }\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic call = bag.Diagnostics[0];
        Assert.Equal("E2001", call.Code);
        Assert.Equal("expected ')' to close call", call.Message);
        Assert.Equal(3, call.Range.Start.Line);
        Assert.Equal(1, call.Range.Start.Column);

        Diagnostic brace = bag.Diagnostics[1];
        Assert.Equal("E2001", brace.Code);
        Assert.Equal("expected '}' to close param block", brace.Message);
        Assert.Equal(3, brace.Range.Start.Line);
        Assert.Equal(1, brace.Range.Start.Column);

        Assert.Equal(2, unit.TopLevel.Count);
        Assert.IsType<ErrorDecl>(unit.TopLevel[0]);
        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }

    [Fact]
    public void MalformedEntry_UnterminatedBody_SubsequentDeclaration_TypeChecksCleanly() {
        const string src = "param {\nx: int = foo(1\nfn good(): int { return 2 }\n";
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Assert.Equal(2, bag.Diagnostics.Count);

        new TypeChecker(bag).Check(unit);

        Assert.Equal(2, bag.Diagnostics.Count);
        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }
}
