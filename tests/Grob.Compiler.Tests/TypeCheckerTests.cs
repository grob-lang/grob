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
        (_, DiagnosticBag bag) = TypeCheckSource("""
            x := 1 + "hello"
            """);
        Assert.True(bag.HasErrors, "Expected a type error for int + string");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        Assert.Equal((1, 6), (error.Range.Start.Line, error.Range.Start.Column));
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
        Assert.Equal((1, 11), (error.Range.Start.Line, error.Range.Start.Column));
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
        Assert.Equal((1, 7), (error.Range.Start.Line, error.Range.Start.Column));
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

    /// <summary>
    /// A unary expression whose operand resolves to <see cref="GrobType.Error"/>
    /// returns <see cref="GrobType.Error"/> immediately (cascade suppression), and
    /// does not emit a second diagnostic for the operator mismatch.
    /// </summary>
    [Fact]
    public void CascadeSuppression_UnaryOnErrorOperand_NoSecondDiagnostic() {
        // "-undefined_var" — VisitIdentifier emits E1001 and returns GrobType.Error.
        // VisitUnary detects operand == GrobType.Error and returns early without a
        // second diagnostic.
        (_, DiagnosticBag bag) = TypeCheckSource("-undefined_var\n");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E1001", error.Code);
        // identifier starts at column 2, immediately after the '-' operator
        Assert.Equal(1, error.Range.Start.Line);
        Assert.Equal(2, error.Range.Start.Column);
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
        Assert.Equal((1, 6), (error.Range.Start.Line, error.Range.Start.Column));
    }

    /// <summary>
    /// The Codex repro: <c>x := missing + 1</c> — the unresolved identifier carries
    /// the <see cref="UnresolvedDecl.Instance"/> sentinel, not null (D-311 / §3.1.1).
    /// </summary>
    [Fact]
    public void UndefinedIdentifier_Declaration_IsUnresolvedDeclSentinel() {
        (CompilationUnit unit, _) = TypeCheckSource("x := missing + 1\n");
        IReadOnlyList<IdentifierExpr> identifiers = CollectIdentifiers(unit);
        IdentifierExpr missing = Assert.Single(identifiers, id => id.Name == "missing");
        Assert.Same(UnresolvedDecl.Instance, missing.Declaration);
    }

    /// <summary>
    /// Three occurrences of the same undefined name each carry the
    /// <see cref="UnresolvedDecl.Instance"/> sentinel — same reference, not just
    /// equal shape — and each independently emit E1001 (cascade suppression applies
    /// to derived type errors, not to the E1001 per reference).
    /// </summary>
    [Fact]
    public void UndefinedIdentifier_MultipleSites_AllShareSentinelInstance() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := missing + missing + missing\n");
        Diagnostic[] errors = bag.Errors.ToArray();
        Assert.Equal(3, errors.Length);
        Assert.Collection(errors,
            e => { Assert.Equal("E1001", e.Code); Assert.Equal((1, 6), (e.Range.Start.Line, e.Range.Start.Column)); },
            e => { Assert.Equal("E1001", e.Code); Assert.Equal((1, 16), (e.Range.Start.Line, e.Range.Start.Column)); },
            e => { Assert.Equal("E1001", e.Code); Assert.Equal((1, 26), (e.Range.Start.Line, e.Range.Start.Column)); });
        IReadOnlyList<IdentifierExpr> unresolved = CollectIdentifiers(unit)
            .Where(id => id.Name == "missing")
            .ToList()
            .AsReadOnly();
        Assert.Equal(3, unresolved.Count);
        Assert.All(unresolved, id => Assert.Same(UnresolvedDecl.Instance, id.Declaration));
    }

    /// <summary>
    /// The §3.1.1 invariant holds across a mixed tree — identifiers that resolve
    /// successfully and identifiers that fail resolution both carry non-null
    /// <see cref="IdentifierExpr.Declaration"/> after type-check.
    /// </summary>
    [Fact]
    public void Declaration_Invariant_HoldsForBothResolvedAndUnresolvedIdentifiers() {
        (CompilationUnit unit, _) = TypeCheckSource("a := 10\nb := a + missing\n");
        IReadOnlyList<IdentifierExpr> identifiers = CollectIdentifiers(unit);
        Assert.NotEmpty(identifiers);
        Assert.All(identifiers, id => Assert.NotNull(id.Declaration));
    }

    // -----------------------------------------------------------------------
    // Control flow — visits all branches (VisitIf, VisitWhile, VisitForIn,
    // VisitSelect, VisitTry).
    // -----------------------------------------------------------------------

    /// <summary>An <c>if/else</c> tree type-checks without error.</summary>
    [Fact]
    public void ControlFlow_If_WithElse_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := 5\nif x < 10 { y := 1 } else { z := 2 }\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A <c>while</c> loop type-checks without error.</summary>
    [Fact]
    public void ControlFlow_While_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := 0\nwhile x < 5 { }\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A <c>for x in xs</c> loop over an array type-checks without error.</summary>
    [Fact]
    public void ControlFlow_ForIn_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("xs := [1, 2, 3]\nfor v in xs { }\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A <c>select</c> statement with cases and a default type-checks without error.</summary>
    [Fact]
    public void ControlFlow_Select_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            x := 1
            select x {
            case 1 { y := 1 }
            default { z := 2 }
            }
            """);
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A <c>try/catch</c> block type-checks without error.</summary>
    [Fact]
    public void ControlFlow_TryCatch_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("try { x := 1 } catch { y := 2 }\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Statements — assignment, compound assignment, increment, expression stmt.
    // -----------------------------------------------------------------------

    /// <summary><c>x = 10</c> after a declaration type-checks without error.</summary>
    [Fact]
    public void Statement_Assignment_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := 5\nx = 10\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary><c>x += 3</c> after a declaration type-checks without error.</summary>
    [Fact]
    public void Statement_CompoundAssignment_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := 5\nx += 3\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary><c>x++</c> after a declaration type-checks without error.</summary>
    [Fact]
    public void Statement_Increment_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := 5\nx++\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A standalone expression inside a function body is a valid expression statement.</summary>
    [Fact]
    public void Statement_ExpressionStmt_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn f(): Void {\n1 + 2\n}\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Declarations — fn with parameters, type, const, readonly, import.
    // -----------------------------------------------------------------------

    /// <summary>A fn with typed parameters infers parameter types and type-checks without error.</summary>
    [Fact]
    public void Declaration_FnWithTypedParams_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn add(a: int, b: int): int { return a + b }\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A <c>type</c> declaration type-checks without error.</summary>
    [Fact]
    public void Declaration_TypeDecl_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("type Point {\nx: int\ny: int\n}\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A <c>const</c> declaration type-checks without error.</summary>
    [Fact]
    public void Declaration_ConstDecl_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("const PI := 3.14\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A <c>readonly</c> declaration with an explicit annotation type-checks without error.</summary>
    [Fact]
    public void Declaration_ReadonlyDecl_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("readonly NAME: string := \"grob\"\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>An <c>import</c> declaration type-checks without error.</summary>
    [Fact]
    public void Declaration_Import_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("import io\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Literals — nil, raw string, regex.
    // -----------------------------------------------------------------------

    /// <summary><c>nil</c> literal resolves to <see cref="GrobType.Nil"/>.</summary>
    [Fact]
    public void Inference_NilLiteral_ResolvesToNil() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := nil\nref := x\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Nil, xRef.ResolvedType);
    }

    /// <summary>A raw-string literal resolves to <see cref="GrobType.String"/>.</summary>
    [Fact]
    public void Inference_RawStringLiteral_ResolvesToString() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := `hello`\nref := x\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.String, xRef.ResolvedType);
    }

    /// <summary>A regex literal type-checks without error.</summary>
    [Fact]
    public void Inference_RegexLiteral_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := /test/\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Interpolated string with an expression part.
    // -----------------------------------------------------------------------

    /// <summary>An interpolated string containing an expression (<c>"${n}"</c>) type-checks without error.</summary>
    [Fact]
    public void InterpolatedString_WithExpressionPart_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("n := 42\ns := \"count: ${n}\"\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Unary operators.
    // -----------------------------------------------------------------------

    /// <summary><c>-int</c> resolves to int.</summary>
    [Fact]
    public void Unary_NegateInt_ResolvesToInt() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := -5\nref := x\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Int, xRef.ResolvedType);
    }

    /// <summary><c>-float</c> resolves to float.</summary>
    [Fact]
    public void Unary_NegateFloat_ResolvesToFloat() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := -1.5\nref := x\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Float, xRef.ResolvedType);
    }

    /// <summary><c>!bool</c> resolves to bool.</summary>
    [Fact]
    public void Unary_NotBool_ResolvesToBool() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("b := !true\nref := b\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr bRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Bool, bRef.ResolvedType);
    }

    /// <summary>Negating a string is a compile error (E0002).</summary>
    [Fact]
    public void Unary_NegateString_IsCompileError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := -\"oops\"\n");
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        Assert.Equal((1, 6), (error.Range.Start.Line, error.Range.Start.Column));
    }

    /// <summary>Logical-not on an int is a compile error (E0002).</summary>
    [Fact]
    public void Unary_NotInt_IsCompileError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := !5\n");
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        Assert.Equal((1, 6), (error.Range.Start.Line, error.Range.Start.Column));
    }

    // -----------------------------------------------------------------------
    // Equality and inequality comparisons.
    // -----------------------------------------------------------------------

    /// <summary><c>int == int</c> resolves to bool.</summary>
    [Fact]
    public void Comparison_IntEqualsInt_ResolvesBool() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("b := 5 == 5\nref := b\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr bRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Bool, bRef.ResolvedType);
    }

    /// <summary>Comparing incompatible types with <c>==</c> is a compile error.</summary>
    [Fact]
    public void Comparison_IntEqualsString_IsCompileError() {
        (_, DiagnosticBag bag) = TypeCheckSource("b := 5 == \"hello\"\n");
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        Assert.Equal((1, 6), (error.Range.Start.Line, error.Range.Start.Column));
    }

    /// <summary>Comparing incompatible types with <c>!=</c> is a compile error.</summary>
    [Fact]
    public void Comparison_IntNotEqualsString_IsCompileError() {
        (_, DiagnosticBag bag) = TypeCheckSource("b := 5 != \"hello\"\n");
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        Assert.Equal((1, 6), (error.Range.Start.Line, error.Range.Start.Column));
    }

    // -----------------------------------------------------------------------
    // Logical operators (&&, ||).
    // -----------------------------------------------------------------------

    /// <summary><c>bool && bool</c> resolves to bool.</summary>
    [Fact]
    public void Logical_AndBothBool_ResolvesBool() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("b := true && false\nref := b\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr bRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Bool, bRef.ResolvedType);
    }

    /// <summary>A non-bool left operand for <c>||</c> is a compile error.</summary>
    [Fact]
    public void Logical_OrNonBoolLeft_IsCompileError() {
        (_, DiagnosticBag bag) = TypeCheckSource("b := 1 || false\n");
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        Assert.Equal((1, 6), (error.Range.Start.Line, error.Range.Start.Column));
    }

    /// <summary>A non-bool right operand for <c>&&</c> is a compile error.</summary>
    [Fact]
    public void Logical_AndNonBoolRight_IsCompileError() {
        (_, DiagnosticBag bag) = TypeCheckSource("b := true && 2\n");
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        Assert.Equal((1, 14), (error.Range.Start.Line, error.Range.Start.Column));
    }

    // -----------------------------------------------------------------------
    // Grouping, call expression, array literal, ternary, numeric range.
    // -----------------------------------------------------------------------

    /// <summary>Grouping passes through the inner expression's type.</summary>
    [Fact]
    public void Expression_Grouping_InnerTypePassesThrough() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := (5 + 3)\nref := x\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
        VarDeclStmt refDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(refDecl.Initializer);
        Assert.Equal(GrobType.Int, xRef.ResolvedType);
    }

    /// <summary>A function call expression type-checks without error.</summary>
    [Fact]
    public void Expression_Call_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn f(): int { return 1 }\nx := f()\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>An array literal type-checks without error.</summary>
    [Fact]
    public void Expression_ArrayLiteral_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("x := [1, 2, 3]\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A ternary expression type-checks without error.</summary>
    [Fact]
    public void Expression_Ternary_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("b := true\nx := b ? 1 : 2\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A numeric range as a <c>for..in</c> iterable type-checks without error.</summary>
    [Fact]
    public void Expression_NumericRange_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("for i in 1..10 step 2 { }\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>A member-access expression type-checks without error.</summary>
    [Fact]
    public void Expression_MemberAccess_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("xs := nil\nn := xs.length\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>An index expression type-checks without error.</summary>
    [Fact]
    public void Expression_Index_NoError() {
        (_, DiagnosticBag bag) = TypeCheckSource("xs := nil\ny := xs[0]\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Annotation with an unknown type (exercises the _ => Unknown branch of
    // ResolveTypeRef) and the nil arithmetic error path (TypeName(Nil)).
    // -----------------------------------------------------------------------

    /// <summary>
    /// An annotation with an unrecognised type name is tolerated: the inferred
    /// type is used instead. This exercises the <c>_ =&gt; Unknown</c> branch of
    /// <see cref="TypeChecker"/>'s <c>ResolveTypeRef</c>.
    /// </summary>
    [Fact]
    public void Annotation_UnknownTypeName_FallsBackToInferredType() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: MyType := 5\n");
        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    /// <summary>
    /// Arithmetic on a <c>nil</c> operand produces E0002 and exercises
    /// <c>TypeName(GrobType.Nil)</c>.
    /// </summary>
    [Fact]
    public void ArithmeticRule_NilPlusInt_IsCompileError() {
        (_, DiagnosticBag bag) = TypeCheckSource("n := nil\nx := n + 5\n");
        Assert.True(bag.HasErrors);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        Assert.Equal((2, 6), (error.Range.Start.Line, error.Range.Start.Column));
    }
}
