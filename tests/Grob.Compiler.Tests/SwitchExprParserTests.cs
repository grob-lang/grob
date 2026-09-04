using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser-layer tests for the switch expression (§3.1) — AST structure for each
/// pattern form, the lambda-vs-arm-arrow disambiguation, trailing-comma
/// acceptance, postfix binding, and malformed-arm recovery. These isolate the
/// parser from the type checker and compiler.
/// </summary>
public sealed class SwitchExprParserTests {
    [Fact]
    public void ValuePatternSwitch_ParsesToSwitchExprNode() {
        Expression e = ExprOf(ParseOk("code switch { 200 => 1, _ => 0 }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);
        Assert.IsType<IdentifierExpr>(sw.Subject);
        Assert.Equal(2, sw.Arms.Count);

        ValuePattern vp = Assert.IsType<ValuePattern>(sw.Arms[0].Pattern);
        Assert.IsType<IntLiteralExpr>(vp.Value);
        Assert.IsType<CatchAllPattern>(sw.Arms[1].Pattern);
    }

    [Fact]
    public void RelationalPattern_ParsesWithOperatorAndOperand() {
        Expression e = ExprOf(ParseOk("n switch { >= 10 => 1, _ => 0 }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);
        RelationalPattern rp = Assert.IsType<RelationalPattern>(sw.Arms[0].Pattern);
        Assert.Equal(BinaryOperator.GreaterEqual, rp.Op);
        Assert.IsType<IntLiteralExpr>(rp.Operand);
    }

    /// <summary>
    /// An identifier pattern followed by <c>=&gt;</c> is the arm arrow, not a lambda
    /// body: <c>x =&gt; 1</c> parses to a value pattern <c>x</c> with result <c>1</c>,
    /// not a <see cref="LambdaExpr"/>. Guards the disambiguation that arm parsing drops
    /// below the lambda precedence level.
    /// </summary>
    [Fact]
    public void IdentifierPatternArrow_IsArmArrowNotLambda() {
        Expression e = ExprOf(ParseOk("n switch { x => 1, _ => 0 }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);

        ValuePattern vp = Assert.IsType<ValuePattern>(sw.Arms[0].Pattern);
        IdentifierExpr id = Assert.IsType<IdentifierExpr>(vp.Value);
        Assert.Equal("x", id.Name);
        Assert.IsType<IntLiteralExpr>(sw.Arms[0].Result);
    }

    [Fact]
    public void TrailingCommaAfterFinalArm_IsAccepted() {
        Expression e = ExprOf(ParseOk("n switch { 1 => 10, _ => 0, }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);
        Assert.Equal(2, sw.Arms.Count);
    }

    /// <summary>The subject binds at the postfix level — a member access is the scrutinee.</summary>
    [Fact]
    public void Subject_IsPostfixExpression() {
        Expression e = ExprOf(ParseOk("obj.field switch { 1 => 10, _ => 0 }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);
        Assert.IsType<MemberAccessExpr>(sw.Subject);
    }

    /// <summary>
    /// A malformed arm with no <c>=&gt;</c> produces an E2001 syntax diagnostic with a
    /// source location.
    /// </summary>
    [Fact]
    public void ArmMissingArrow_ProducesE2001() {
        (_, DiagnosticBag bag) = Parse("x := n switch { 1 10 }\n");
        // The root-cause diagnostic points at the token where '=>' was expected.
        Assert.Contains(bag.Errors,
            d => d.Code == "E2001" && d.Range.Start.Line == 1 && d.Range.Start.Column == 19);
    }

    // -----------------------------------------------------------------------
    // Error recovery — malformed arm (D-406, closing the finding D-405 left open
    // for switch-expression arms). ParseSwitchArmOrError (comma-boundary mode)
    // mirrors ParseMapEntryOrError/ParseFieldInitOrError exactly.
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedArm_MissingArrow_RecoversWithOneDiagnostic() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := n switch { 1 10, _ => 0 }\ny := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected '=>' after switch pattern", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(19, d.Range.Start.Column); // the stray '10'

        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        SwitchArm arm = Assert.Single(sw.Arms);
        Assert.IsType<CatchAllPattern>(arm.Pattern);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("y", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    /// <summary>
    /// Load-bearing (D-405 shape): proves the fix removes the phantom duplicate
    /// without suppressing a second, genuinely independent mistake in the same arm
    /// list.
    /// </summary>
    [Fact]
    public void MalformedArm_TwoDistinctMalformedArms_ProducesTwoDiagnosticsNoneSwallowed() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := n switch { 1 10, 2 20, _ => 0 }\ny := 3\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic first = bag.Diagnostics[0];
        Assert.Equal("E2001", first.Code);
        Assert.Equal("expected '=>' after switch pattern", first.Message);
        Assert.Equal(1, first.Range.Start.Line);
        Assert.Equal(19, first.Range.Start.Column);

        Diagnostic second = bag.Diagnostics[1];
        Assert.Equal("E2001", second.Code);
        Assert.Equal("expected '=>' after switch pattern", second.Message);
        Assert.Equal(1, second.Range.Start.Line);
        Assert.Equal(25, second.Range.Start.Column);

        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        SwitchArm arm = Assert.Single(sw.Arms);
        Assert.IsType<CatchAllPattern>(arm.Pattern);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("y", tail.Name);
        Assert.Equal(3L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedArm_SubsequentStatement_TypeChecksCleanly() {
        const string src = "x := 1 switch { 1 10, _ => 0 }\ny := 2\nz := y + 1\n";
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Diagnostic parseDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", parseDiagnostic.Code);

        new TypeChecker(bag).Check(unit);

        Diagnostic onlyDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Same(parseDiagnostic, onlyDiagnostic);

        VarDeclStmt zDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        BinaryExpr sum = Assert.IsType<BinaryExpr>(zDecl.Initializer);
        IdentifierExpr yRef = Assert.IsType<IdentifierExpr>(sum.Left);
        Assert.Equal("y", yRef.Name);
        Assert.NotEqual(GrobType.Error, yRef.ResolvedType);
        Assert.NotNull(yRef.Declaration);
        Assert.NotSame(UnresolvedDecl.Instance, yRef.Declaration);
    }

    // -----------------------------------------------------------------------
    // Error recovery — delimiters the abandoned arm opened before it failed
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedArm_ResultClosesItsOwnBracketPair_RecoversAtOuterCommaOnly() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := n switch { 1 => foo(1 2), _ => 0 }\ny := 3\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(28, d.Range.Start.Column); // the stray '2'

        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        SwitchArm arm = Assert.Single(sw.Arms);
        Assert.IsType<CatchAllPattern>(arm.Pattern);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("y", tail.Name);
        Assert.Equal(3L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedArm_ResultLeavesBracketPairOpen_DoesNotReuseInnerComma() {
        // The comma inside the still-open '(' now raises E2209 (D-421 Decision 2)
        // rather than the pre-D-421 generic "expected ')'" — the recovery
        // mechanics this test pins are otherwise unchanged.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("x := n switch { 1 => (1, _ => 0 }\nq := 9\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2209", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(24, d.Range.Start.Column); // the ',' inside the still-open '('

        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        Assert.Empty(sw.Arms);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("q", tail.Name);
        Assert.Equal(9L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedArm_UnterminatedAfterMalformedArm_ReportsBothRootCauses() {
        // EOF safety (malformed input never throws, recovery never loops): a
        // malformed arm AND a missing closing '}' are two genuinely independent
        // mistakes; both must be reported.
        (_, DiagnosticBag bag) = Parse("x := n switch { 1 10\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic arrow = bag.Diagnostics[0];
        Assert.Equal("E2001", arrow.Code);
        Assert.Equal("expected '=>' after switch pattern", arrow.Message);
        Assert.Equal(1, arrow.Range.Start.Line);
        Assert.Equal(19, arrow.Range.Start.Column);

        Diagnostic brace = bag.Diagnostics[1];
        Assert.Equal("E2001", brace.Code);
        Assert.Equal("expected '}' to close switch body", brace.Message);
        Assert.Equal(2, brace.Range.Start.Line);
        Assert.Equal(1, brace.Range.Start.Column);
    }
}
