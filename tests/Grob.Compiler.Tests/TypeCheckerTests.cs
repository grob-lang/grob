using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Tests for the two-pass type checker (Sprint 2 Increment C).
/// Covers type inference on <c>:=</c>, arithmetic and comparison type rules,
/// the <see cref="IdentifierExpr.ResolvedType"/> and
/// <see cref="IdentifierExpr.Declaration"/> invariants (§3.1.1), the
/// collect-all-errors contract, cascade suppression via the Error type, and
/// two-pass forward references (D-166).
/// </summary>
public sealed class TypeCheckerTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Parses <paramref name="source"/> and runs the type checker against it.
    /// Returns the annotated compilation unit and the diagnostic bag.
    /// </summary>
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag);
        checker.Check(unit);
        return (unit, bag);
    }

    /// <summary>Traverses an AST and collects every <see cref="IdentifierExpr"/>.</summary>
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
    // Type inference on :=
    // -----------------------------------------------------------------------

    /// <summary><c>x := 2 + 3</c> — int + int → int; x resolves to int.</summary>
    [Fact]
    public void Inference_IntPlusInt_ResolvesToInt() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := 2 + 3\nref := x\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Int, xRef.ResolvedType);
    }

    /// <summary><c>y := 2.0 + 3</c> — float + int → float (implicit promotion); y resolves to float.</summary>
    [Fact]
    public void Inference_FloatPlusInt_ResolvesToFloat() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("y := 2.0 + 3\nref := y\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr yRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Float, yRef.ResolvedType);
    }

    /// <summary><c>s := "a" + "b"</c> — string + string → string; s resolves to string.</summary>
    [Fact]
    public void Inference_StringPlusString_ResolvesToString() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            s := "a" + "b"
            ref := s
            """);
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr sRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.String, sRef.ResolvedType);
    }

    // -----------------------------------------------------------------------
    // Arithmetic type rules
    // -----------------------------------------------------------------------

    /// <summary><c>int + string</c> is a compile error with a source location.</summary>
    [Fact]
    public void ArithmeticRule_IntPlusString_IsCompileError() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            x := 1 + "hello"
            """);
        Assert.True(bag.HasErrors, "Expected a type error for int + string");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        // Source location must be populated — not Unknown.
        Assert.NotEqual(SourceRange.Unknown, error.Range);
    }

    /// <summary><c>int / int</c> resolves to int (truncating per spec).</summary>
    [Fact]
    public void ArithmeticRule_IntDivideInt_ResolvesToInt() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("q := 7 / 2\nref := q\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr qRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Int, qRef.ResolvedType);
    }

    /// <summary>int and float in mixed arithmetic resolve to float.</summary>
    [Fact]
    public void ArithmeticRule_IntTimesFloat_ResolvesToFloat() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("v := 3 * 1.5\nref := v\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr vRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Float, vRef.ResolvedType);
    }

    // -----------------------------------------------------------------------
    // Explicit annotation validation
    // -----------------------------------------------------------------------

    /// <summary><c>x: float := 3</c> — int widened to float via annotation; no error.</summary>
    [Fact]
    public void Annotation_IntToFloat_IsValid() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: float := 3\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary><c>x: int := 3.0</c> — float cannot narrow to int; E0001.</summary>
    [Fact]
    public void Annotation_FloatToInt_IsCompileError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: int := 3.0\n");
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0001", error.Code);
    }

    // -----------------------------------------------------------------------
    // Comparison rules — result type is bool
    // -----------------------------------------------------------------------

    /// <summary>A comparison expression resolves to bool.</summary>
    [Fact]
    public void Comparison_IntLessInt_ResolvesBool() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("ok := 3 < 5\nref := ok\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr okRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Bool, okRef.ResolvedType);
    }

    /// <summary>Comparing a string against an int is a compile error.</summary>
    [Fact]
    public void Comparison_StringVsInt_IsCompileError() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            ok := "hello" < 5
            """);
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 ResolvedType invariant
    // -----------------------------------------------------------------------

    /// <summary>
    /// After type-checking a representative tree with only scalar variables,
    /// every <see cref="IdentifierExpr"/> carries a <see cref="GrobType"/>
    /// other than <see cref="GrobType.Unknown"/>.
    /// </summary>
    [Fact]
    public void ResolvedType_Invariant_AllIdentifiersHaveNonUnknownType() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            x := 5
            y := 3
            z := x + y
            w := z * 2
            """);
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        IReadOnlyList<IdentifierExpr> identifiers = CollectIdentifiers(unit);
        Assert.NotEmpty(identifiers);
        Assert.All(identifiers, id =>
            Assert.NotEqual(GrobType.Unknown, id.ResolvedType));
    }

    // -----------------------------------------------------------------------
    // §3.1.1 Declaration back-reference invariant
    // -----------------------------------------------------------------------

    /// <summary>
    /// After type-checking a representative tree, every <see cref="IdentifierExpr"/>
    /// carries a non-<see langword="null"/> <see cref="IdentifierExpr.Declaration"/>
    /// pointing to the declaring AST node.
    /// </summary>
    [Fact]
    public void Declaration_Invariant_AllIdentifiersHaveNonNullDeclaration() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            a := 10
            b := 20
            c := a + b
            """);
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        IReadOnlyList<IdentifierExpr> identifiers = CollectIdentifiers(unit);
        Assert.NotEmpty(identifiers);
        Assert.All(identifiers, id => Assert.NotNull(id.Declaration));
    }

    /// <summary>
    /// The <see cref="IdentifierExpr.Declaration"/> for each reference points back to
    /// the exact <see cref="VarDeclStmt"/> node that declared the name.
    /// </summary>
    [Fact]
    public void Declaration_PointsToDeclaringNode() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := 42\nresult := x\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));

        // The VarDeclStmt for 'x' is the expected declaring node.
        VarDeclStmt xDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[0]);
        VarDeclStmt resultDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(resultDecl.Initializer);

        Assert.Same(xDecl, xRef.Declaration);
    }

    // -----------------------------------------------------------------------
    // Two-pass forward reference (D-166)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A function body that calls a function declared later in the same file
    /// type-checks without error. The pass-1 registration of the later function
    /// makes it visible to the pass-2 validation of the earlier body (D-166).
    /// </summary>
    [Fact]
    public void TwoPass_ForwardReference_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn first(): int {
              return second()
            }
            fn second(): int {
              return 42
            }
            """);
        // No "undefined identifier" for 'second', even though it is declared after 'first'.
        Assert.False(bag.HasErrors,
            $"Expected no errors for a valid forward reference, but got: {ParserTestHelpers.FormatDiagnostics(bag)}");
    }

    // -----------------------------------------------------------------------
    // Collect-all errors (never stop at the first)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When a compilation unit contains three independent type errors, all three
    /// are reported in the diagnostic bag — not just the first.
    /// </summary>
    [Fact]
    public void CollectAll_ThreeIndependentErrors_AllReported() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            a := 1 + "oops"
            b := true + 5
            c := "text" - "text"
            """);
        Assert.Equal(3, bag.Errors.Count());
    }

    // -----------------------------------------------------------------------
    // Cascade suppression via the Error type
    // -----------------------------------------------------------------------

    /// <summary>
    /// An expression built on an already-errored sub-expression resolves to Error
    /// and does not emit a second, derived diagnostic. Only the original error
    /// is reported.
    /// </summary>
    [Fact]
    public void CascadeSuppression_ErroredSubExpr_NoSecondDiagnostic() {
        // Line 1 produces E0002 (int + string).
        // Line 2: x resolves to Error, so x + 2 is suppressed — no second error.
        (_, DiagnosticBag bag) = TypeCheckSource("x := 1 + \"oops\"\ny := x + 2\n");
        Assert.Single(bag.Errors);
    }

    // -----------------------------------------------------------------------
    // Undefined identifier
    // -----------------------------------------------------------------------

    /// <summary>Referencing an undeclared name produces E1001 with a source location.</summary>
    [Fact]
    public void UndefinedIdentifier_EmitsE1001() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := missing + 1\n");
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E1001", error.Code);
        Assert.NotEqual(SourceRange.Unknown, error.Range);
    }
}
