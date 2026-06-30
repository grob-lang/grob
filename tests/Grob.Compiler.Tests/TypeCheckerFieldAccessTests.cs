using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 6 Increment C — struct field read and write.
/// Covers: field-type annotation on <see cref="MemberAccessExpr"/>, nested access
/// resolution, E1002 (undefined member), E0204 (mutation of readonly binding) and
/// E0001 (field-assignment type mismatch).
/// </summary>
public sealed class TypeCheckerFieldAccessTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private sealed class MemberAccessCollector : AstWalker {
        public List<MemberAccessExpr> Nodes { get; } = [];
        public override Unit VisitMemberAccess(MemberAccessExpr node) {
            Nodes.Add(node);
            Visit(node.Target);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static List<MemberAccessExpr> CollectMemberAccesses(CompilationUnit unit) {
        var collector = new MemberAccessCollector();
        collector.Visit(unit);
        return collector.Nodes;
    }

    // -----------------------------------------------------------------------
    // ResolvedFieldType annotation — field reads on a struct binding
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAccess_KnownStringField_AnnotatesResolvedFieldType() {
        var (unit, bag) = TypeCheckSource("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "example.com", port: 8080 }
            readonly h := c.host
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        List<MemberAccessExpr> accesses = CollectMemberAccesses(unit);
        MemberAccessExpr ma = Assert.Single(accesses);
        Assert.Equal("host", ma.Member);
        Assert.Equal(GrobType.String, ma.ResolvedFieldType);
    }

    [Fact]
    public void FieldAccess_KnownIntField_AnnotatesResolvedFieldType() {
        var (unit, bag) = TypeCheckSource("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "x", port: 8080 }
            readonly p := c.port
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        List<MemberAccessExpr> accesses = CollectMemberAccesses(unit);
        MemberAccessExpr ma = Assert.Single(accesses);
        Assert.Equal(GrobType.Int, ma.ResolvedFieldType);
    }

    // -----------------------------------------------------------------------
    // ResolvedStructTypeName — when the field's own type is a user-defined struct
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAccess_StructTypedField_AnnotatesResolvedStructTypeName() {
        var (unit, bag) = TypeCheckSource("""
            type Address {
            city: string
            }
            type Person {
            name: string
            address: Address
            }
            readonly p := Person { name: "Alice", address: Address { city: "London" } }
            readonly addr := p.address
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        List<MemberAccessExpr> accesses = CollectMemberAccesses(unit);
        MemberAccessExpr ma = Assert.Single(accesses);
        Assert.Equal("address", ma.Member);
        Assert.Equal(GrobType.Struct, ma.ResolvedFieldType);
        Assert.Equal("Address", ma.ResolvedStructTypeName);
    }

    // -----------------------------------------------------------------------
    // Nested access — p.address.city resolves each step
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAccess_NestedStructField_ResolvesEachStep() {
        var (unit, bag) = TypeCheckSource("""
            type Address {
            city: string
            }
            type Person {
            name: string
            address: Address
            }
            readonly p := Person { name: "Alice", address: Address { city: "London" } }
            readonly city := p.address.city
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        List<MemberAccessExpr> accesses = CollectMemberAccesses(unit);
        // Collector visits outermost first, then recurses into Target.
        // city_node (outer) is at index 0; address_node (inner) is at index 1.
        Assert.Equal(2, accesses.Count);
        MemberAccessExpr cityAccess = accesses[0];
        MemberAccessExpr addressAccess = accesses[1];
        Assert.Equal("city", cityAccess.Member);
        Assert.Equal(GrobType.String, cityAccess.ResolvedFieldType);
        Assert.Equal("address", addressAccess.Member);
        Assert.Equal(GrobType.Struct, addressAccess.ResolvedFieldType);
        Assert.Equal("Address", addressAccess.ResolvedStructTypeName);
    }

    // -----------------------------------------------------------------------
    // E1002 — undefined member
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAccess_UndefinedField_EmitsE1002() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            }
            readonly c := Config { host: "x" }
            readonly v := c.nosuchfield
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // E0204 — mutation of readonly binding via field assignment
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAssign_ReadonlyBinding_EmitsE0204() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "example.com", port: 8080 }
            c.host = "new"
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Equal(6, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void FieldAssign_ReadonlyBinding_NestedChain_EmitsE0204() {
        // Deep immutability: even `p.address.city = "x"` is E0204 when `p` is readonly.
        DiagnosticBag bag = Check("""
            type Address {
            city: string
            }
            type Person {
            name: string
            address: Address
            }
            readonly p := Person { name: "Alice", address: Address { city: "London" } }
            p.address.city = "Paris"
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Equal(9, diag.Range.Start.Line);
    }

    // -----------------------------------------------------------------------
    // Field-assignment type mismatch (E0001)
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAssign_TypeMismatch_EmitsE0001() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            port: int
            }
            c := Config { host: "example.com", port: 8080 }
            c.port = "not an int"
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Equal(6, diag.Range.Start.Line);
        Assert.Equal(10, diag.Range.Start.Column);
    }

    [Fact]
    public void FieldAssign_CompatibleType_NoErrors() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            port: int
            }
            c := Config { host: "example.com", port: 8080 }
            c.port = 9090
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void FieldAssign_MutableBinding_NoErrors() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            port: int
            }
            c := Config { host: "example.com", port: 8080 }
            c.host = "localhost"
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Unknown-typed RHS must not emit a spurious assignability error
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAssign_UnknownRhsType_NoSpuriousError() {
        // An array index access returns GrobType.Unknown at the type-checker layer.
        // Assigning that to a typed field must not emit a spurious E0001/E0104 —
        // the field assignment path must skip assignability when RHS is Unknown,
        // matching the behaviour of the identifier-assignment path.
        DiagnosticBag bag = Check("""
            type Config {
            port: int
            }
            c := Config { port: 8080 }
            arr := [1, 2, 3]
            c.port = arr[0]
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Explicit type annotation exercises ExtractFromBinding → ExtractStructName
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAccess_WithExplicitTypeAnnotation_ResolvesCorrectly() {
        // An explicit struct-type annotation on the binding goes through
        // ExtractFromBinding(annotation, sc) → ExtractStructName(annotation).
        // The result must be the same as without an annotation.
        var (unit, bag) = TypeCheckSource("""
            type Config {
            port: int
            }
            readonly c: Config := Config { port: 8080 }
            readonly p := c.port
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        List<MemberAccessExpr> accesses = CollectMemberAccesses(unit);
        MemberAccessExpr ma = Assert.Single(accesses);
        Assert.Equal(GrobType.Int, ma.ResolvedFieldType);
    }

    // -----------------------------------------------------------------------
    // E0206 — optional chaining in assignment target
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAssign_OptionalChain_EmitsE0206() {
        DiagnosticBag bag = Check("""
            type Config {
            port: int
            }
            c := Config { port: 8080 }
            c?.port = 9090
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0206", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // F3 guard — '?.' on any nullable receiver returns Unknown without E0101
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAccess_OptionalChainOnNullableScalar_NoError() {
        // '?.' on a NullableString target must not emit E0101 (that fires only for
        // plain '.') and must not attempt struct-field resolution — the F3 guard
        // returns Unknown immediately, making the result permissive downstream.
        DiagnosticBag bag = Check("""
            fn maybeLabel(): string? {
                return nil
            }
            x := maybeLabel()
            y := x?.length
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Assignability short-circuit when field or RHS type is Error
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAssign_ToUndefinedField_EmitsE1002Only() {
        // Visit(memberTarget) emits E1002 and returns GrobType.Error.
        // The assignability guard must short-circuit on fieldType=Error so no
        // spurious E0001 is added on top of the field-not-found diagnostic.
        DiagnosticBag bag = Check("""
            type Config {
            port: int
            }
            c := Config { port: 8080 }
            c.nonexistent = 5
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void FieldAssign_ErrorTypedRhs_NoSpuriousAssignabilityError() {
        // Visit(node.Value) emits E1001 (undefined identifier) and returns Error.
        // The assignability guard must short-circuit on rhsType=Error so no
        // spurious E0001 is stacked on top of the undefined-identifier diagnostic.
        DiagnosticBag bag = Check("""
            type Config {
            port: int
            }
            c := Config { port: 8080 }
            c.port = undeclaredVariable
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(10, diag.Range.Start.Column);
    }
}
