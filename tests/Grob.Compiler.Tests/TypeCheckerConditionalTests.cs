using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 4 Increment A — conditionals:
/// <c>if</c>/<c>else if</c>/<c>else</c>, <c>&amp;&amp;</c>/<c>||</c> (type-rule
/// side already done in Sprint 3D via <see cref="TypeChecker.ResolveLogical"/>)
/// and the ternary <c>?:</c>.
/// </summary>
/// <remarks>
/// Covers: bool-condition enforcement on <c>if</c> and ternary (E0001),
/// ternary arm-unification (E0001 on mismatch; Int/Float widening),
/// and the §3.1.1 invariant — identifier nodes in conditional trees carry
/// non-null <see cref="IdentifierExpr.ResolvedType"/> and
/// <see cref="IdentifierExpr.Declaration"/> after type-check.
/// </remarks>
public sealed class TypeCheckerConditionalTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag);
        checker.Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    /// <summary>Traverses the AST and collects every <see cref="IdentifierExpr"/>.</summary>
    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) {
            Identifiers.Add(node);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static IReadOnlyList<IdentifierExpr> CollectIdentifiers(CompilationUnit unit) {
        IdentifierCollector collector = new();
        collector.Visit(unit);
        return collector.Identifiers;
    }

    // -----------------------------------------------------------------------
    // if — bool-condition enforcement
    // -----------------------------------------------------------------------

    /// <summary>
    /// A non-<c>bool</c> condition in an <c>if</c> statement must produce E0001
    /// pointing at the condition expression.
    /// <para>Source: <c>if (42) { }</c> — condition type <c>int</c>.</para>
    /// </summary>
    [Fact]
    public void IfStmt_NonBoolCondition_EmitsE0001() {
        DiagnosticBag bag = Check("if (42) { }");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0001", error.Code);
        Assert.Equal(1, error.Range.Start.Line);
        Assert.Equal(4, error.Range.Start.Column);  // GroupingExpr '(42)' starts at '(' = column 4
    }

    /// <summary>
    /// A non-<c>bool</c> condition in an <c>else if</c> arm must produce E0001
    /// on the inner condition, not on the outer <c>if</c>.
    /// <para>Source: <c>if (true) { } else if (42) { }</c>.</para>
    /// </summary>
    [Fact]
    public void IfElseIf_NonBoolCondition_SecondArm_EmitsE0001() {
        DiagnosticBag bag = Check("if (true) { } else if (42) { }");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0001", error.Code);
        Assert.Equal(1, error.Range.Start.Line);
        Assert.Equal(23, error.Range.Start.Column);  // GroupingExpr '(42)' starts at '(' = column 23
    }

    /// <summary>
    /// A valid <c>bool</c> condition in an <c>if</c> statement produces no diagnostics.
    /// </summary>
    [Fact]
    public void IfStmt_BoolCondition_NoErrors() {
        DiagnosticBag bag = Check("if (true) { }");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// Two independent non-bool conditions (one per arm) produce two separate E0001
    /// diagnostics — the checker never stops at the first error.
    /// </summary>
    [Fact]
    public void IfElseIf_BothArmsNonBool_EmitsTwoE0001() {
        DiagnosticBag bag = Check("if (42) { } else if (99) { }");
        Assert.Equal(2, bag.Errors.Count());
        Assert.All(bag.Errors, e => Assert.Equal("E0001", e.Code));
    }

    // -----------------------------------------------------------------------
    // Ternary — bool-condition enforcement
    // -----------------------------------------------------------------------

    /// <summary>
    /// A non-<c>bool</c> ternary condition must produce E0001 pointing at the condition.
    /// <para>Source: <c>x := 42 ? 1 : 2</c> — condition type <c>int</c>.</para>
    /// </summary>
    [Fact]
    public void TernaryExpr_NonBoolCondition_EmitsE0001() {
        DiagnosticBag bag = Check("x := 42 ? 1 : 2");
        Assert.True(bag.HasErrors, "Expected E0001 for non-bool ternary condition");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0001", error.Code);
        Assert.Equal(1, error.Range.Start.Line);
        Assert.Equal(6, error.Range.Start.Column);  // '42' starts at column 6 after 'x := '
    }

    // -----------------------------------------------------------------------
    // Ternary — arm unification
    // -----------------------------------------------------------------------

    /// <summary>
    /// Ternary arms with incompatible types (int and string) must produce E0001.
    /// </summary>
    [Fact]
    public void TernaryExpr_ArmsDoNotUnify_EmitsE0001() {
        DiagnosticBag bag = Check("""x := true ? 1 : "bad" """);
        Assert.True(bag.HasErrors, "Expected E0001 for non-unifying ternary arms");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0001", error.Code);
        Assert.Equal(1, error.Range.Start.Line);
    }

    /// <summary>
    /// Two <c>int</c> arms unify to <c>int</c>; the ternary expression resolves to <c>int</c>.
    /// </summary>
    [Fact]
    public void TernaryExpr_IntArms_ReturnsInt() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := true ? 1 : 2\nref := x\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Int, xRef.ResolvedType);
    }

    /// <summary>
    /// An <c>int</c> arm and a <c>float</c> arm unify to <c>float</c> (implicit widening).
    /// </summary>
    [Fact]
    public void TernaryExpr_IntAndFloat_ReturnsFloat() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := true ? 1 : 2.0\nref := x\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Float, xRef.ResolvedType);
    }

    /// <summary>
    /// Two matching string arms unify to <c>string</c>.
    /// </summary>
    [Fact]
    public void TernaryExpr_StringArms_ReturnsString() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            x := true ? "a" : "b"
            ref := x
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.String, xRef.ResolvedType);
    }

    /// <summary>
    /// Ternary with a bool condition and two bool arms produces no diagnostics.
    /// </summary>
    [Fact]
    public void TernaryExpr_BoolConditionAndBoolArms_NoErrors() {
        DiagnosticBag bag = Check("x := true ? false : true");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // §3.1.1 — identifier nodes in conditional trees carry LSP fields
    // -----------------------------------------------------------------------

    /// <summary>
    /// After type-checking a program with an identifier inside an <c>if</c>
    /// condition, that identifier carries a non-null <see cref="IdentifierExpr.ResolvedType"/>
    /// and a non-null <see cref="IdentifierExpr.Declaration"/> (§3.1.1 invariant).
    /// </summary>
    [Fact]
    public void IfStmt_IdentifierInCondition_HasLspFields() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            flag := true
            if (flag) { }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));

        IReadOnlyList<IdentifierExpr> ids = CollectIdentifiers(unit);
        IdentifierExpr flagInCond = ids.Single(id => id.Name == "flag");

        Assert.NotEqual(GrobType.Unknown, flagInCond.ResolvedType);
        Assert.Equal(GrobType.Bool, flagInCond.ResolvedType);
        Assert.NotNull(flagInCond.Declaration);
        Assert.IsType<VarDeclStmt>(flagInCond.Declaration);
    }

    /// <summary>
    /// After type-checking a ternary expression where both arms reference an
    /// identifier, all identifier nodes carry non-null LSP fields (§3.1.1).
    /// </summary>
    [Fact]
    public void TernaryExpr_IdentifierInArms_HasLspFields() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            a := 1
            b := 2
            x := true ? a : b
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));

        IReadOnlyList<IdentifierExpr> ids = CollectIdentifiers(unit);
        foreach (IdentifierExpr id in ids.Where(id => id.Name is "a" or "b")) {
            Assert.NotEqual(GrobType.Unknown, id.ResolvedType);
            Assert.NotNull(id.Declaration);
        }
    }

    /// <summary>
    /// An identifier in a <c>&amp;&amp;</c> operand carries the §3.1.1 LSP fields.
    /// (The operand type-rule — both operands must be bool — is already tested by
    /// the Sprint 3D logical-operator tests; this test is the LSP-guard only.)
    /// </summary>
    [Fact]
    public void LogicalAnd_IdentifierOperand_HasLspFields() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            ok := true
            result := ok && true
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));

        IReadOnlyList<IdentifierExpr> ids = CollectIdentifiers(unit);
        // VarDeclStmt LHS is a Name string, not an IdentifierExpr — only the use-site inside '&&' is collected.
        IdentifierExpr okRef = ids.Single(id => id.Name == "ok");
        Assert.Equal(GrobType.Bool, okRef.ResolvedType);
        Assert.NotNull(okRef.Declaration);
    }
}
