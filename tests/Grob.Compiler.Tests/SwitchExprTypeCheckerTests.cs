using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 4 Increment E — the switch <b>expression</b>
/// <c>value switch { pattern =&gt; result, _ =&gt; default }</c>.
/// </summary>
/// <remarks>
/// The switch expression is the exhaustive, value-producing counterpart to the
/// non-exhaustive <c>select</c> statement (D-301). The type checker proves
/// exhaustiveness (§3.1) — a non-exhaustive switch is <see cref="ErrorCatalog.E0505"/>
/// — type-checks each pattern against the scrutinee (D-277) and unifies all arm
/// results to one type, reusing Increment A's ternary arm unification.
/// </remarks>
public sealed class SwitchExprTypeCheckerTests {
    private static DiagnosticBag TypeCheckSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    private static CompilationUnit ParseAndCheck(string source, out DiagnosticBag bag) {
        bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return unit;
    }

    // -----------------------------------------------------------------------
    // Exhaustiveness — E0505
    // -----------------------------------------------------------------------

    /// <summary>
    /// A switch over <c>int</c> value patterns with no <c>_</c> arm cannot be proven
    /// exhaustive (int is not finitely enumerable), so it is E0505 at the expression.
    /// </summary>
    [Fact]
    public void NonExhaustive_NoCatchAll_ProducesE0505() {
        DiagnosticBag bag = TypeCheckSource("y := 1 switch { 2 => 20, 3 => 30 }");
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E0505");
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    /// <summary>A <c>_</c> catch-all makes any switch exhaustive.</summary>
    [Fact]
    public void CatchAll_IsExhaustive_NoError() {
        DiagnosticBag bag = TypeCheckSource("y := 1 switch { 1 => 10, _ => 0 }");
        Assert.False(bag.HasErrors);
    }

    /// <summary>
    /// A <c>bool</c> scrutinee with both <c>true</c> and <c>false</c> value arms is
    /// exhaustive without a <c>_</c> arm (§3.1).
    /// </summary>
    [Fact]
    public void BoolBothArms_IsExhaustive_NoError() {
        DiagnosticBag bag = TypeCheckSource("b := true\ny := b switch { true => 1, false => 2 }");
        Assert.False(bag.HasErrors);
    }

    /// <summary>A <c>bool</c> scrutinee matching only <c>true</c> is non-exhaustive — E0505.</summary>
    [Fact]
    public void BoolOnlyTrueArm_ProducesE0505() {
        DiagnosticBag bag = TypeCheckSource("b := true\ny := b switch { true => 1 }");
        Diagnostic diag = Assert.Single(bag.Errors, e => e.Code == "E0505");
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    /// <summary>
    /// Relational patterns never contribute to exhaustiveness (§3.1); a switch built
    /// from relational arms alone, with no <c>_</c>, is E0505.
    /// </summary>
    [Fact]
    public void RelationalArmsWithoutCatchAll_ProducesE0505() {
        DiagnosticBag bag = TypeCheckSource("y := 5 switch { >= 10 => 1, >= 3 => 2 }");
        Assert.Single(bag.Errors, e => e.Code == "E0505");
    }

    /// <summary>A relational switch closed by <c>_</c> is exhaustive.</summary>
    [Fact]
    public void RelationalArmsWithCatchAll_NoError() {
        DiagnosticBag bag = TypeCheckSource("y := 5 switch { >= 10 => 1, _ => 0 }");
        Assert.False(bag.HasErrors);
    }

    /// <summary>
    /// A nullable scrutinee is exhaustive when <c>nil</c> is matched and the element
    /// type is otherwise covered — here <c>bool?</c> with <c>nil</c>, <c>true</c> and
    /// <c>false</c>.
    /// </summary>
    [Fact]
    public void NullableBoolWithNilAndBothArms_IsExhaustive_NoError() {
        DiagnosticBag bag = TypeCheckSource(
            "b: bool? := nil\ny := b switch { nil => 0, true => 1, false => 2 }");
        Assert.False(bag.HasErrors);
    }

    // -----------------------------------------------------------------------
    // Pattern type-checking (D-277, §3.1 Types)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A value pattern whose type is not assignable to the scrutinee is E0001 at the
    /// pattern — here a <c>string</c> pattern against an <c>int</c> scrutinee.
    /// </summary>
    [Fact]
    public void ValuePatternTypeMismatch_ProducesE0001() {
        DiagnosticBag bag = TypeCheckSource("y := 1 switch { 2 => 10, \"x\" => 20, _ => 0 }");
        Assert.Single(bag.Errors, e => e.Code == "E0001");
    }

    /// <summary>A <c>nil</c> value pattern against a non-nullable scrutinee is E0001.</summary>
    [Fact]
    public void NilPatternOnNonNullableScrutinee_ProducesE0001() {
        DiagnosticBag bag = TypeCheckSource("y := 1 switch { nil => 10, _ => 0 }");
        Assert.Single(bag.Errors, e => e.Code == "E0001");
    }

    /// <summary>
    /// A relational pattern is legal only on an ordered scrutinee; a relational arm on
    /// a <c>bool</c> scrutinee is E0001.
    /// </summary>
    [Fact]
    public void RelationalPatternOnBoolScrutinee_ProducesE0001() {
        DiagnosticBag bag = TypeCheckSource("b := true\ny := b switch { >= false => 1, _ => 0 }");
        Assert.Single(bag.Errors, e => e.Code == "E0001");
    }

    // -----------------------------------------------------------------------
    // Arm unification (reuses Increment A's UnifyTernaryArms)
    // -----------------------------------------------------------------------

    /// <summary>Arms that do not unify to one type are E0001.</summary>
    [Fact]
    public void NonUnifyingArms_ProducesE0001() {
        DiagnosticBag bag = TypeCheckSource("y := 1 switch { 1 => 10, _ => \"x\" }");
        Assert.Single(bag.Errors, e => e.Code == "E0001");
    }

    /// <summary>
    /// <c>int</c> and <c>float</c> arms unify to <c>float</c> — the same widening the
    /// ternary uses — so the switch type-checks clean.
    /// </summary>
    [Fact]
    public void IntAndFloatArms_UnifyWithoutError() {
        DiagnosticBag bag = TypeCheckSource("y := 1 switch { 1 => 10, _ => 2.5 }");
        Assert.False(bag.HasErrors);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant — every identifier carries ResolvedType and Declaration
    // -----------------------------------------------------------------------

    /// <summary>
    /// The §3.1.1 invariant holds inside a switch expression: the subject identifier
    /// and an identifier value pattern both carry a non-null
    /// <see cref="IdentifierExpr.ResolvedType"/> and <see cref="IdentifierExpr.Declaration"/>
    /// after type-check.
    /// </summary>
    [Fact]
    public void IdentifierSubjectAndPattern_AreResolved() {
        CompilationUnit unit = ParseAndCheck(
            "a := 5\nb := 3\nx := a switch { b => 10, _ => 0 }\n", out DiagnosticBag bag);
        Assert.False(bag.HasErrors);

        SwitchExprNode sw = unit.TopLevel
            .OfType<VarDeclStmt>()
            .Select(v => v.Initializer)
            .OfType<SwitchExprNode>()
            .Single();

        var subject = Assert.IsType<IdentifierExpr>(sw.Subject);
        Assert.Equal(GrobType.Int, subject.ResolvedType);
        Assert.NotNull(subject.Declaration);

        var valuePattern = Assert.IsType<ValuePattern>(sw.Arms[0].Pattern);
        var patternId = Assert.IsType<IdentifierExpr>(valuePattern.Value);
        Assert.Equal(GrobType.Int, patternId.ResolvedType);
        Assert.NotNull(patternId.Declaration);
    }
}
