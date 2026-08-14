using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser recovery tests for <c>type</c>-declaration field bodies (D-406, closing
/// the finding D-405 left open for this construct). A <c>type</c> body is a
/// <b>newline-separated</b>, brace-delimited element list — the declaration-context
/// sibling of D-405's comma-separated literal interiors — so it uses
/// <c>ParseTypeFieldOrError</c>/<c>SkipToNextLiteralElementBoundary</c>'s newline
/// mode, which additionally anchors on a top-level declaration keyword (unlike the
/// comma mode, which deliberately runs to true EOF for a literal).
/// </summary>
public sealed class ParserTypeDeclRecoveryTests {
    [Fact]
    public void MalformedField_MissingColon_RecoversWithOneDiagnostic() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("type Foo {\nbad\ny: int\n}\nx := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ':' after field name", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(4, d.Range.Start.Column);

        TypeDecl decl = Assert.IsType<TypeDecl>(unit.TopLevel[0]);
        Assert.Equal("Foo", decl.Name);
        TypeField field = Assert.Single(decl.Fields);
        Assert.Equal("y", field.Name);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    /// <summary>
    /// Load-bearing (D-405 shape): proves the fix removes the phantom duplicate
    /// without suppressing a second, genuinely independent mistake in the same
    /// body.
    /// </summary>
    [Fact]
    public void MalformedField_TwoDistinctMalformedFields_ProducesTwoDiagnosticsNoneSwallowed() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("type Foo {\nbad\nbad2\ny: int\n}\nx := 2\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic first = bag.Diagnostics[0];
        Assert.Equal("E2001", first.Code);
        Assert.Equal("expected ':' after field name", first.Message);
        Assert.Equal(2, first.Range.Start.Line);
        Assert.Equal(4, first.Range.Start.Column);

        Diagnostic second = bag.Diagnostics[1];
        Assert.Equal("E2001", second.Code);
        Assert.Equal("expected ':' after field name", second.Message);
        Assert.Equal(3, second.Range.Start.Line);
        Assert.Equal(5, second.Range.Start.Column);

        TypeDecl decl = Assert.IsType<TypeDecl>(unit.TopLevel[0]);
        TypeField field = Assert.Single(decl.Fields);
        Assert.Equal("y", field.Name);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedField_SubsequentDeclaration_TypeChecksCleanly() {
        const string src = "type Foo {\nbad\ny: int\n}\nfn good(): int { return 1 }\n";
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
    // Error recovery — delimiters the abandoned field opened before it failed
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedField_ValueClosesItsOwnBracketPair_RecoversAtNextFieldOnly() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("type Foo {\nx: int = foo(1 2)\ny: int\n}\nfn good(): int { return 2 }\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(16, d.Range.Start.Column); // the stray '2'

        TypeDecl decl = Assert.IsType<TypeDecl>(unit.TopLevel[0]);
        TypeField field = Assert.Single(decl.Fields);
        Assert.Equal("y", field.Name);

        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }

    [Fact]
    public void MalformedField_ValueLeavesBracketPairOpen_DoesNotFabricateNextField() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("type Foo {\nx: int = foo(1\ny: int\n}\nfn good(): int { return 2 }\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column); // '}' cannot close call, so the scan runs

        TypeDecl decl = Assert.IsType<TypeDecl>(unit.TopLevel[0]);
        // 'y: int' was swallowed inside the still-open '(' — not fabricated as a
        // second field — so the type body closes with no fields at all.
        Assert.Empty(decl.Fields);

        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }

    [Fact]
    public void MalformedField_UnterminatedAfterMalformedField_ReportsBothRootCauses() {
        // EOF safety (malformed input never throws, recovery never loops): a
        // malformed field AND a missing closing '}' are two genuinely independent
        // mistakes; both must be reported.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("type Foo {\nbad\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic colon = bag.Diagnostics[0];
        Assert.Equal("E2001", colon.Code);
        Assert.Equal("expected ':' after field name", colon.Message);
        Assert.Equal(2, colon.Range.Start.Line);
        Assert.Equal(4, colon.Range.Start.Column);

        Diagnostic brace = bag.Diagnostics[1];
        Assert.Equal("E2001", brace.Code);
        Assert.Equal("expected '}' to close type body", brace.Message);
        Assert.Equal(3, brace.Range.Start.Line);
        Assert.Equal(1, brace.Range.Start.Column);

        Assert.NotNull(unit);
    }

    // -----------------------------------------------------------------------
    // Keyword anchor — the newline-boundary variant's own confirmed design point
    // -----------------------------------------------------------------------

    /// <summary>
    /// A malformed field with an unterminated default-value expression (no closing
    /// '}' for the type body at all) followed by a further top-level declaration
    /// must not swallow that declaration. Before this fix's keyword-anchor gate,
    /// the loop would re-attempt a field parse at the unconsumed 'fn' keyword and
    /// force-advance past it (the zero-consumption-progress guard every recovery
    /// wrapper shares), eating the keyword the resync had deliberately stopped at.
    /// </summary>
    [Fact]
    public void MalformedField_UnterminatedBody_RecoversAtNextTopLevelKeyword() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("type Foo {\nx: int = foo(1\nfn good(): int { return 2 }\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic call = bag.Diagnostics[0];
        Assert.Equal("E2001", call.Code);
        Assert.Equal("expected ')' to close call", call.Message);
        Assert.Equal(3, call.Range.Start.Line);
        Assert.Equal(1, call.Range.Start.Column);

        Diagnostic brace = bag.Diagnostics[1];
        Assert.Equal("E2001", brace.Code);
        Assert.Equal("expected '}' to close type body", brace.Message);
        Assert.Equal(3, brace.Range.Start.Line);
        Assert.Equal(1, brace.Range.Start.Column);

        Assert.Equal(2, unit.TopLevel.Count);
        Assert.IsType<ErrorDecl>(unit.TopLevel[0]);
        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }

    [Fact]
    public void MalformedField_UnterminatedBody_SubsequentDeclaration_TypeChecksCleanly() {
        const string src = "type Foo {\nx: int = foo(1\nfn good(): int { return 2 }\n";
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Assert.Equal(2, bag.Diagnostics.Count);

        new TypeChecker(bag).Check(unit);

        // No further diagnostics beyond the two parser-level root causes — the
        // recovered 'fn good' declaration type-checks cleanly.
        Assert.Equal(2, bag.Diagnostics.Count);
        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }
}
