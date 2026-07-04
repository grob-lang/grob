using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 6 Increment D — anonymous struct literals.
/// Covers structural type synthesis, structural identity, projection through
/// lambdas/.select(), nested anonymous structs, E1002 (undefined field),
/// E2101 (bare brace), and the §3.1.1 invariant.
/// </summary>
public sealed class TypeCheckerAnonStructTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private sealed class AnonStructCollector : AstWalker {
        public List<AnonStructExpr> Nodes { get; } = [];
        public override Unit VisitAnonStruct(AnonStructExpr node) { Nodes.Add(node); return default; }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) { Identifiers.Add(node); return default; }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    // -----------------------------------------------------------------------
    // Structural type synthesis — field names and types
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_SimpleFields_SynthesisesStructuralType() {
        var (unit, bag) = TypeCheckSource("""
            readonly p := #{ name: "Alice", salary: 50000 }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");

        var collector = new AnonStructCollector();
        collector.Visit(unit);
        AnonStructExpr anon = Assert.Single(collector.Nodes);
        Assert.NotNull(anon.SynthesisedTypeName);
        Assert.Contains("name", anon.SynthesisedTypeName);
        Assert.Contains("salary", anon.SynthesisedTypeName);
    }

    // -----------------------------------------------------------------------
    // Field access — type-safe against synthesised type
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_FieldAccess_NoError() {
        DiagnosticBag bag = Check("""
            readonly p := #{ name: "Alice", salary: 50000 }
            readonly n := p.name
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void AnonStruct_UndefinedFieldAccess_EmitsE1002() {
        DiagnosticBag bag = Check("""
            readonly p := #{ name: "Alice" }
            readonly x := p.missing
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Structural identity — same field set shares structural type
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_SameFieldSet_SharesStructuralType() {
        var (unit, bag) = TypeCheckSource("""
            readonly a := #{ name: "Alice", age: 30 }
            readonly b := #{ name: "Bob",   age: 25 }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");

        var collector = new AnonStructCollector();
        collector.Visit(unit);
        Assert.Equal(2, collector.Nodes.Count);
        Assert.Equal(collector.Nodes[0].SynthesisedTypeName, collector.Nodes[1].SynthesisedTypeName);
    }

    [Fact]
    public void AnonStruct_DifferentFieldSets_DifferentStructuralType() {
        var (unit, bag) = TypeCheckSource("""
            readonly a := #{ name: "Alice" }
            readonly b := #{ age: 30 }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");

        var collector = new AnonStructCollector();
        collector.Visit(unit);
        Assert.Equal(2, collector.Nodes.Count);
        Assert.NotEqual(collector.Nodes[0].SynthesisedTypeName, collector.Nodes[1].SynthesisedTypeName);
    }

    // -----------------------------------------------------------------------
    // Projections through .select()
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_SelectProjection_NoError() {
        DiagnosticBag bag = Check("""
            readonly employees := [#{ name: "Alice", salary: 50000 }]
            readonly names := employees.select(e => e.name)
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void AnonStruct_SelectToAnonStruct_NoError() {
        DiagnosticBag bag = Check("""
            readonly employees := [#{ name: "Alice", salary: 50000 }]
            readonly projected := employees.select(e => #{ name: e.name, salary: e.salary })
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Nested anonymous structs
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_Nested_ResolvesRecursively() {
        DiagnosticBag bag = Check("""
            readonly body := #{ properties: #{ mode: "Incremental" } }
            readonly m := body.properties.mode
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void AnonStruct_Nested_UndefinedDeepField_EmitsE1002() {
        DiagnosticBag bag = Check("""
            readonly body := #{ properties: #{ mode: "Incremental" } }
            readonly x := body.properties.missing
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Inline access (literal.field)
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_InlineFieldAccess_NoError() {
        DiagnosticBag bag = Check("""
            readonly n := #{ name: "Alice" }.name
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Brace disambiguation — E2101
    // -----------------------------------------------------------------------

    [Fact]
    public void BareBrace_InExpressionPosition_EmitsE2101() {
        DiagnosticBag bag = Check("""
            readonly x := { name: "Alice" }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E2101", diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant — all identifiers inside field values carry resolved state
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_FieldValues_IdentifiersFullyResolved() {
        var (unit, bag) = TypeCheckSource("""
            readonly age := 30
            readonly p := #{ name: "Alice", years: age }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");

        var collector = new IdentifierCollector();
        collector.Visit(unit);

        foreach (IdentifierExpr id in collector.Identifiers) {
            Assert.NotEqual(GrobType.Unknown, id.ResolvedType);
            Assert.NotNull(id.Declaration);
        }
    }

    // -----------------------------------------------------------------------
    // Layer-invariant Theory — pathological inputs never throw
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("#{ }")]                               // empty anonymous struct
    [InlineData("#{ a: 1 }")]                          // single field
    [InlineData("#{ a: 1, b: 2, c: 3, d: 4, e: 5 }")] // many fields
    [InlineData("#{ a: #{ b: #{ c: 1 } } }")]          // deeply nested
    public void AnonStruct_ValidInputs_TypeCheckWithoutThrowing(string expr) {
        var ex = Record.Exception(() => Check($"readonly x := {expr}"));
        Assert.Null(ex);
    }

    // -----------------------------------------------------------------------
    // Nested anonymous structs with different shapes are not structural equals
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_NestedWithDifferentShape_DifferentStructuralTypes() {
        var (unit, bag) = TypeCheckSource("""
            readonly a := #{ inner: #{ x: 1 } }
            readonly b := #{ inner: #{ y: 1 } }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");

        var collector = new AnonStructCollector();
        collector.Visit(unit);
        Assert.Equal(2, collector.Nodes.Count);
        Assert.NotEqual(collector.Nodes[0].SynthesisedTypeName, collector.Nodes[1].SynthesisedTypeName);
    }

    // -----------------------------------------------------------------------
    // Inline struct-construction member access (StructConstructionExpr as target)
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_InlineAccess_UndefinedField_EmitsE1002() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            port: int
            }
            readonly x := Config { host: "example.com", port: 8080 }.nonexistent
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Theory]
    [InlineData("#{ .bad: 1 }")]             // invalid field name syntax
    [InlineData("#{ a: missing_var }")]      // undefined value reference
    public void AnonStruct_MalformedInputs_ProducesDiagnosticsWithoutThrowing(string expr) {
        DiagnosticBag bag = null!;
        var ex = Record.Exception(() => bag = Check($"readonly x := {expr}"));
        Assert.Null(ex);
        Assert.True(bag.HasErrors);
    }
}
