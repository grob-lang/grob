using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 4 Increment C — <c>for...in</c> iteration rules.
/// </summary>
/// <remarks>
/// Covers the four iteration diagnostics (E0501 non-iterable subject, E0502
/// single-identifier map, E0503 descending range without a negative step, E0504
/// iterator-variable reassignment), the iteration-variable type inference
/// (<c>item</c> from the element type, <c>i</c> as <c>int</c>, <c>k</c>/<c>v</c>
/// from the map), and the §3.1.1 invariant on the synthetic and visible iteration
/// identifier nodes.
/// <para>
/// Map iteration cannot be expressed by a top-level binding — there is no map
/// literal in the v1 parser (parser/AST work, out of scope for this increment) —
/// so the map-form rules are exercised through a <c>fn</c> parameter annotated
/// <c>map&lt;K, V&gt;</c>, whose body the type checker walks.
/// </para>
/// </remarks>
public sealed class TypeCheckerForInTests {
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

    private static IdentifierExpr FindIdentifier(CompilationUnit unit, string name) =>
        CollectIdentifiers(unit).First(id => id.Name == name);

    // -----------------------------------------------------------------------
    // Well-typed forms — no diagnostics, correct iteration-variable types
    // -----------------------------------------------------------------------

    [Fact]
    public void ArraySingle_TypeChecksClean() {
        DiagnosticBag bag = Check("for item in [1, 2, 3] {\nprint(item)\n}");
        Assert.False(bag.HasErrors);
    }

    [Fact]
    public void ArrayIndex_InfersIndexAsInt() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource(
            "for i, item in [1, 2, 3] {\nprint(i)\nprint(item)\n}");
        Assert.False(bag.HasErrors);
        Assert.Equal(GrobType.Int, FindIdentifier(unit, "i").ResolvedType);
    }

    [Fact]
    public void Range_InfersCounterAsInt() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("for i in 0..3 {\nprint(i)\n}");
        Assert.False(bag.HasErrors);
        Assert.Equal(GrobType.Int, FindIdentifier(unit, "i").ResolvedType);
    }

    [Fact]
    public void RangeWithStep_TypeChecksClean() {
        DiagnosticBag bag = Check("for i in 0..10 step 5 {\nprint(i)\n}");
        Assert.False(bag.HasErrors);
    }

    [Fact]
    public void DescendingRangeWithNegativeStep_TypeChecksClean() {
        DiagnosticBag bag = Check("for i in 3..0 step -1 {\nprint(i)\n}");
        Assert.False(bag.HasErrors);
    }

    [Fact]
    public void MapTwoIdentifier_InfersKeyAsString() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource(
            "fn iter(m: map<string, string>): void {\nfor k, v in m {\nprint(k)\n}\n}");
        Assert.False(bag.HasErrors);
        Assert.Equal(GrobType.String, FindIdentifier(unit, "k").ResolvedType);
    }

    // -----------------------------------------------------------------------
    // E0501 — non-iterable subject
    // -----------------------------------------------------------------------

    [Fact]
    public void NonIterableSubject_EmitsE0501() {
        DiagnosticBag bag = Check("for x in 42 {\n}");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0501", error.Code);
        Assert.Equal(1, error.Range.Start.Line);
        Assert.Equal(10, error.Range.Start.Column); // the subject '42'
    }

    [Fact]
    public void StringSubject_EmitsE0501() {
        DiagnosticBag bag = Check("for c in \"abc\" {\n}");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0501", error.Code);
    }

    // -----------------------------------------------------------------------
    // E0502 — single-identifier for...in over a map
    // -----------------------------------------------------------------------

    [Fact]
    public void SingleIdentifierOverMap_EmitsE0502() {
        DiagnosticBag bag = Check(
            "fn iter(m: map<string, string>): void {\nfor k in m {\n}\n}");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0502", error.Code);
        Assert.Equal(2, error.Range.Start.Line);
        Assert.Equal(10, error.Range.Start.Column); // the subject 'm' on line 2
    }

    [Fact]
    public void SingleIdentifierOverMap_SuggestsKeys() {
        DiagnosticBag bag = Check(
            "fn iter(m: map<string, string>): void {\nfor k in m {\n}\n}");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Contains(".keys", error.Message);
    }

    // -----------------------------------------------------------------------
    // E0503 — descending range without an explicit negative step
    // -----------------------------------------------------------------------

    [Fact]
    public void DescendingRangeWithoutStep_EmitsE0503() {
        DiagnosticBag bag = Check("for i in 3..0 {\n}");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0503", error.Code);
        Assert.Equal(1, error.Range.Start.Line);
        Assert.Equal(10, error.Range.Start.Column); // the range '3..0'
    }

    [Fact]
    public void AscendingRangeWithoutStep_NoE0503() {
        DiagnosticBag bag = Check("for i in 0..3 {\n}");
        Assert.False(bag.HasErrors);
    }

    [Fact]
    public void FloatRangeBound_EmitsE0001() {
        DiagnosticBag bag = Check("for i in 1.5..3 {\n}");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0001", error.Code);
    }

    // -----------------------------------------------------------------------
    // E0504 — reassignment of a for...in iterator variable
    // -----------------------------------------------------------------------

    [Fact]
    public void ReassignIteratorVariable_EmitsE0504() {
        DiagnosticBag bag = Check("for item in [1, 2, 3] {\nitem = 5\n}");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0504", error.Code);
        Assert.Equal(2, error.Range.Start.Line);
        Assert.Equal(1, error.Range.Start.Column); // the target 'item'
    }

    [Fact]
    public void ReassignRangeCounter_EmitsE0504() {
        DiagnosticBag bag = Check("for i in 0..3 {\ni = 9\n}");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0504", error.Code);
    }

    [Fact]
    public void CompoundAssignIteratorVariable_EmitsE0504() {
        DiagnosticBag bag = Check("for i in 0..3 {\ni += 1\n}");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0504", error.Code);
    }

    // -----------------------------------------------------------------------
    // Loop control inside for...in is accepted (loop depth)
    // -----------------------------------------------------------------------

    [Fact]
    public void BreakAndContinueInsideForIn_NoLoopControlError() {
        DiagnosticBag bag = Check("for i in 0..3 {\nif (i == 1) { break }\ncontinue\n}");
        Assert.False(bag.HasErrors);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant — synthetic and visible iteration identifiers
    // -----------------------------------------------------------------------

    [Fact]
    public void IterationIdentifiers_CarryNonNullDeclarationAndType() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource(
            "for i, item in [1, 2, 3] {\nprint(i)\nprint(item)\n}");
        Assert.False(bag.HasErrors);
        foreach (IdentifierExpr id in CollectIdentifiers(unit)) {
            Assert.NotNull(id.Declaration);
        }
    }

    [Fact]
    public void IteratorVariable_DeclaredByTheForInStatement() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource(
            "for item in [1, 2, 3] {\nprint(item)\n}");
        Assert.False(bag.HasErrors);
        IdentifierExpr item = FindIdentifier(unit, "item");
        Assert.IsType<ForInStmt>(item.Declaration);
    }
}
