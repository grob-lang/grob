using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser recovery tests for named-struct construction (<c>TypeName { field: value, … }</c>,
/// <see cref="Ast.Expressions.StructConstructionExpr"/>) — D-406, closing the finding
/// D-405 left open for this call site. Reuses <c>ParseFieldInitOrError</c>/
/// <c>SkipToNextLiteralElementBoundary</c> unchanged (comma-boundary mode) — see the
/// map-literal/anon-struct twins in <c>ParserMapLiteralTests.cs</c>/
/// <c>ParserAnonStructLiteralTests.cs</c> for the mechanism.
/// </summary>
/// <remarks>
/// <c>LooksLikeStructConstruction()</c> only inspects the shape of the <b>first</b>
/// field (identifier immediately followed by ':', or an empty body) before committing
/// to struct-construction parsing — it never inspects the value. A malformed field
/// <b>name</b> can therefore only be reproduced from the second field onward (the
/// first field must satisfy the lookahead); a malformed field <b>value</b> can be the
/// first field, since the lookahead never looks past the colon. Both shapes are real,
/// independent reproductions of the same underlying gap (any <c>ParseOneFieldInit</c>
/// failure not locally caught), so both are exercised below.
/// </remarks>
public sealed class ParserStructConstructionRecoveryTests {
    [Fact]
    public void MalformedField_SecondFieldNonIdentifierName_RecoversWithOneDiagnostic() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := Point { a: 1, 2: 2 }\ny := 3\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected field name", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(20, d.Range.Start.Column); // the stray '2'

        StructConstructionExpr sc = Assert.IsType<StructConstructionExpr>(
            Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        Assert.Equal("Point", sc.TypeName);
        FieldInit field = Assert.Single(sc.Fields);
        Assert.Equal("a", field.Name);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("y", tail.Name);
        Assert.Equal(3L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    /// <summary>
    /// Load-bearing (D-405 shape): proves the fix removes the phantom duplicate
    /// without suppressing a second, genuinely independent mistake in the same
    /// construction.
    /// </summary>
    [Fact]
    public void MalformedField_TwoDistinctMalformedNames_ProducesTwoDiagnosticsNoneSwallowed() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := Point { a: 1, 2: 2, 3: 3 }\ny := 4\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic first = bag.Diagnostics[0];
        Assert.Equal("E2001", first.Code);
        Assert.Equal("expected field name", first.Message);
        Assert.Equal(1, first.Range.Start.Line);
        Assert.Equal(20, first.Range.Start.Column);

        Diagnostic second = bag.Diagnostics[1];
        Assert.Equal("E2001", second.Code);
        Assert.Equal("expected field name", second.Message);
        Assert.Equal(1, second.Range.Start.Line);
        Assert.Equal(26, second.Range.Start.Column);

        StructConstructionExpr sc = Assert.IsType<StructConstructionExpr>(
            Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        FieldInit field = Assert.Single(sc.Fields);
        Assert.Equal("a", field.Name);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("y", tail.Name);
        Assert.Equal(4L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedField_SubsequentStatement_TypeChecksCleanly() {
        const string src = "x := Point { a: 1, 2: 2 }\ny := 3\nz := y + 1\n";
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Diagnostic parseDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", parseDiagnostic.Code);

        new TypeChecker(bag).Check(unit);

        // A construction of an undeclared type 'Point' is itself a genuine,
        // independent type-checker diagnostic — unrelated to this fix — so this
        // asserts recovery only on the well-formed tail, not zero further errors.
        VarDeclStmt zDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        BinaryExpr sum = Assert.IsType<BinaryExpr>(zDecl.Initializer);
        IdentifierExpr yRef = Assert.IsType<IdentifierExpr>(sum.Left);
        Assert.Equal("y", yRef.Name);
        Assert.NotEqual(GrobType.Error, yRef.ResolvedType);
        Assert.NotNull(yRef.Declaration);
        Assert.NotSame(UnresolvedDecl.Instance, yRef.Declaration);
    }

    // -----------------------------------------------------------------------
    // Error recovery — delimiters the abandoned field opened before it failed
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedField_ValueClosesItsOwnBracketPair_RecoversAtOuterCommaOnly() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := Point { a: foo(1 2), b: 3 }\nq := 9\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(23, d.Range.Start.Column); // the stray '2'

        StructConstructionExpr sc = Assert.IsType<StructConstructionExpr>(
            Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        FieldInit field = Assert.Single(sc.Fields);
        Assert.Equal("b", field.Name);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("q", tail.Name);
        Assert.Equal(9L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedField_ValueLeavesBracketPairOpen_DoesNotReuseInnerComma() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := Point { a: (1, b: 2 }\nq := 9\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')'", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(19, d.Range.Start.Column); // the ',' inside the still-open '('

        StructConstructionExpr sc = Assert.IsType<StructConstructionExpr>(
            Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        Assert.Empty(sc.Fields);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("q", tail.Name);
        Assert.Equal(9L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedField_UnterminatedAfterMalformedName_ReportsBothRootCauses() {
        // EOF safety (malformed input never throws, recovery never loops): a
        // malformed second field AND a missing closing '}' are two genuinely
        // independent mistakes; both must be reported, not silently absorbed.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := Point { a: 1, 2: 2\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic name = bag.Diagnostics[0];
        Assert.Equal("E2001", name.Code);
        Assert.Equal("expected field name", name.Message);
        Assert.Equal(1, name.Range.Start.Line);
        Assert.Equal(20, name.Range.Start.Column);

        Diagnostic brace = bag.Diagnostics[1];
        Assert.Equal("E2001", brace.Code);
        Assert.Equal("expected '}' to close struct construction", brace.Message);
        Assert.Equal(2, brace.Range.Start.Line);
        Assert.Equal(1, brace.Range.Start.Column);

        Assert.NotNull(unit);
    }
}
