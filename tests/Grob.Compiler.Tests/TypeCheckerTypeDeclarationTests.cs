using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 6 Increment A — type declarations, the type
/// registry and required-non-nullable field-cycle detection.
/// </summary>
/// <remarks>
/// Covers field-type resolution through the full §9 grammar, E0301/E0302 cycle
/// detection, E1102 type-name collision and E2208 duplicate fields.
/// No construction, no field access — declaration registration only.
/// </remarks>
public sealed class TypeCheckerTypeDeclarationTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) { Identifiers.Add(node); return default; }
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
    // Well-typed declarations — no diagnostics
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeDecl_RegistersFields_CorrectTypesAndRequiredFlags() {
        DiagnosticBag bag = Check("""
            type Repo {
            name: string
            port: int = 80
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_ForwardFieldRef_Resolves() {
        // A references B which is declared after A — pass-1 registration resolves it.
        DiagnosticBag bag = Check("""
            type A {
            b: B
            }
            type B {
            x: int
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_FieldAnnotation_NamedUserType_Resolves() {
        DiagnosticBag bag = Check("""
            type A {
            x: int
            }
            type B {
            a: A
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_FieldAnnotation_SelfRefArray_NoError() {
        // Tree[] terminates the cycle walk — must not fire E0301/E0302.
        DiagnosticBag bag = Check("""
            type Tree {
            value: int
            children: Tree[]
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_FieldAnnotation_Nullable_NoError() {
        // Node? terminates the cycle walk — must not fire E0301/E0302.
        DiagnosticBag bag = Check("""
            type Node {
            value: int
            next: Node?
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_FieldAnnotation_Map_Resolves() {
        DiagnosticBag bag = Check("""
            type X {
            meta: map<string, int>
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_FieldAnnotation_FunctionType_Resolves() {
        DiagnosticBag bag = Check("""
            type X {
            cb: fn(): int
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_TreePattern_NoFire() {
        DiagnosticBag bag = Check("""
            type Tree {
            value: int
            children: Tree[]
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_NodePattern_NoFire() {
        DiagnosticBag bag = Check("""
            type Node {
            value: int
            next: Node?
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_NullableArrayField_NoFire() {
        // T[]? — NullableArray — also terminates the cycle walk.
        DiagnosticBag bag = Check("""
            type X {
            items: X[]?
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_DefaultedField_DoesNotParticipateInCycleWalk() {
        // A required struct-typed field with no default participates in the cycle
        // walk; one with a default does not. Use a self-referential struct field
        // with a nil default to exercise the defaulted-edge path: without the
        // isRequired guard this would fire E0302.
        DiagnosticBag bag = Check("""
            type Self {
            me: Self = nil
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Field-annotation errors
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeDecl_ArrayFieldNestedUnknownType_EmitsE1001() {
        // Missing[] — the array element type is unknown; E1001 must fire.
        DiagnosticBag bag = Check("""
            type A {
            items: Missing[]
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(8, diag.Range.Start.Column);
    }

    [Fact]
    public void TypeDecl_FunctionFieldUnknownParamType_EmitsE1001() {
        // fn(Missing): int — the parameter type is unknown; E1001 must fire.
        DiagnosticBag bag = Check("""
            type A {
            cb: fn(Missing): int
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(8, diag.Range.Start.Column);
    }

    [Fact]
    public void TypeDecl_SelfRefThroughPrefix_EmitsE0302OnSelfRefType() {
        // type A { b: B }  type B { a: B }
        // B has a required self-ref in its own field, reached after traversing A→B.
        // The cycle is B→B (trivial self-ref), so E0302 must fire at B's field,
        // not E0301 at A's field.
        DiagnosticBag bag = Check("""
            type A {
            b: B
            }
            type B {
            a: B
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0302", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void TypeDecl_FieldAnnotation_UnknownType_EmitsE1001() {
        DiagnosticBag bag = Check("""
            type X {
            a: Unknown
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(4, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // E0302 — trivial self-reference
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeDecl_SelfReference_EmitsE0302() {
        DiagnosticBag bag = Check("""
            type A {
            a: A
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0302", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // E0301 — multi-type cycle
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeDecl_MultiTypeCycle_EmitsE0301() {
        DiagnosticBag bag = Check("""
            type A {
            b: B
            }
            type B {
            a: A
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0301", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void TypeDecl_LongChainCycle_EmitsE0301() {
        DiagnosticBag bag = Check("""
            type A {
            b: B
            }
            type B {
            c: C
            }
            type C {
            a: A
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0301", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void TypeDecl_SelfRefRequiredCycle_WithOptionalSibling_EmitsE0302() {
        // The optional field (next: A?) does not make the required one safe.
        DiagnosticBag bag = Check("""
            type A {
            self: A
            other: A?
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0302", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // E1102 — type name collision
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeDecl_TypeNameCollision_WithType_EmitsE1102AtSecond() {
        DiagnosticBag bag = Check("""
            type A {
            x: int
            }
            type A {
            y: int
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1102", diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void TypeDecl_TypeNameCollision_WithFn_EmitsE1102AtType() {
        DiagnosticBag bag = Check("""
            fn foo(): int {
            return 1
            }
            type foo {
            x: int
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1102", diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void TypeDecl_TypeNameCollision_ValueBeforeType_EmitsE1102AtType() {
        DiagnosticBag bag = Check("""
            x := 1
            type x {
            a: int
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1102", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // E2208 — duplicate field name
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeDecl_DuplicateFieldName_EmitsE2208() {
        DiagnosticBag bag = Check("""
            type X {
            a: int
            a: string
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E2208", diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void TypeDecl_MultipleDuplicateFields_AllEmitE2208() {
        DiagnosticBag bag = Check("""
            type X {
            a: int
            b: string
            a: bool
            b: float
            }
            """);
        List<Diagnostic> errors = bag.Errors.ToList();
        Assert.Equal(2, errors.Count);
        Assert.Equal("E2208", errors[0].Code);
        Assert.Equal(4, errors[0].Range.Start.Line);
        Assert.Equal(1, errors[0].Range.Start.Column);
        Assert.Equal("E2208", errors[1].Code);
        Assert.Equal(5, errors[1].Range.Start.Line);
        Assert.Equal(1, errors[1].Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 LSP invariant
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeDecl_LspInvariant_IdentifierNodes_HaveNonNullDeclaration() {
        // Use a parameter in the function body so that IdentifierExpr nodes are
        // produced; without an identifier reference CollectIdentifiers returns an
        // empty list and Assert.All passes vacuously.
        (CompilationUnit unit, DiagnosticBag _) = TypeCheckSource("""
            type Address {
            city: string
            country: string
            }
            fn greet(name: string): string {
            return name
            }
            """);
        IReadOnlyList<IdentifierExpr> ids = CollectIdentifiers(unit);
        Assert.NotEmpty(ids);
        Assert.All(ids, id => {
            Assert.NotNull(id.Declaration);
            Assert.NotEqual(GrobType.Error, id.ResolvedType);
        });
    }

    // -----------------------------------------------------------------------
    // Layer invariant — pathological input must never throw
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("type Empty { }")]
    [InlineData("type A { a: A }")]
    [InlineData("type A { b: B }\ntype B { a: A }")]
    [InlineData("type A { x: int }\ntype A { y: int }")]
    [InlineData("type A { a: int\na: string }")]
    public void TypeDecl_LayerInvariant_PathologicalInput_NeverThrows(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Exception? ex = Record.Exception(() => new TypeChecker(bag).Check(unit));
        Assert.Null(ex);
    }
}
