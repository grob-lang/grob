using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 6 Increment B — named struct construction.
/// Covers E0103 (missing required field), E0012 (unknown field name), E0013
/// (field default references sibling field), E2102 (type name used bare in
/// expression position), and the §3.1.1 invariant.
/// </summary>
public sealed class TypeCheckerStructConstructionTests {
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

    // -----------------------------------------------------------------------
    // E0012 — unknown field name
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_UnknownFieldName_EmitsE0012() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            }
            readonly c := Config { typo: "x" }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0012", diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(24, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // E0103 — missing required field
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_MissingRequiredField_EmitsE0103() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "example.com" }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0103", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void StructConstruction_AllRequiredFieldsSupplied_NoError() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "example.com", port: 8080 }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void StructConstruction_DefaultedFieldOmitted_NoError() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            port: int = 80
            }
            readonly c := Config { host: "example.com" }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Multiple errors — two-mode check
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_MultipleErrors_EmitsAll() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            }
            readonly c := Config { typo: "x", another: "y" }
            """);

        // Both unknown fields must be reported — never stops at first.
        List<Diagnostic> errors = bag.Errors.ToList();
        Assert.Equal(2, errors.Count);
        Assert.All(errors, d => Assert.Equal("E0012", d.Code));
        Assert.Equal(4, errors[0].Range.Start.Line);
        Assert.Equal(24, errors[0].Range.Start.Column);  // 'typo'
        Assert.Equal(4, errors[1].Range.Start.Line);
        Assert.Equal(35, errors[1].Range.Start.Column);  // 'another'
    }

    [Fact]
    public void StructConstruction_MissingRequiredAndUnknown_BothReported() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { typo: 99 }
            """);

        // E0012 for the unknown field, E0103 for the excess missing required field.
        List<Diagnostic> errors = bag.Errors.ToList();
        Assert.Equal(2, errors.Count);
        Diagnostic e0012 = Assert.Single(errors, d => d.Code == "E0012");
        Diagnostic e0103 = Assert.Single(errors, d => d.Code == "E0103");
        Assert.Equal(5, e0012.Range.Start.Line);
        Assert.Equal(24, e0012.Range.Start.Column);  // 'typo' on line 5
        Assert.Equal(5, e0103.Range.Start.Line);
        Assert.Equal(15, e0103.Range.Start.Column);  // 'Config' construction expression
    }

    // -----------------------------------------------------------------------
    // E0013 — field default references sibling field
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeDecl_DefaultReferencesSiblingField_EmitsE0013() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            label: string = host
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0013", diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(17, diag.Range.Start.Column);  // 'host' identifier in the default expression
    }

    [Fact]
    public void TypeDecl_DefaultReferencesNonSiblingIdentifier_NoError() {
        DiagnosticBag bag = Check("""
            const defaultHost := "localhost"
            type Config {
            host: string = defaultHost
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // E2102 — type name used bare in expression position (no braces)
    // -----------------------------------------------------------------------

    [Fact]
    public void StructName_UsedBareInExpression_EmitsE2102() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string = "localhost"
            }
            print(Config)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E2102", diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);  // 'Config' inside print(...)
    }

    // -----------------------------------------------------------------------
    // Nested construction
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_Nested_NoError() {
        DiagnosticBag bag = Check("""
            type Inner {
            x: int
            }
            type Outer {
            inner: Inner
            }
            readonly o := Outer { inner: Inner { x: 42 } }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant — every identifier node has ResolvedType + Declaration
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_IdentifierNodes_HaveNonNullResolvedTypeAndDeclaration() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            type Config {
            host: string
            port: int = 80
            }
            readonly h := "example.com"
            readonly c := Config { host: h }
            """);

        IdentifierCollector collector = new();
        collector.Visit(unit);

        foreach (IdentifierExpr id in collector.Identifiers) {
            // ResolvedType must never be Unknown after a successful parse (§3.1.1).
            // Resolved identifiers carry a concrete type; error paths carry GrobType.Error.
            Assert.True(id.ResolvedType != GrobType.Unknown || bag.HasErrors,
                $"Identifier '{id.Name}' at {id.Range} has UnknownType after type-check.");
            // D-311: Declaration is never null after type-check; error paths use the
            // UnresolvedDecl.Instance sentinel rather than null.
            Assert.NotSame(UnresolvedDecl.Instance, id.Declaration);
            Assert.NotNull(id.Declaration);
        }
    }

    // -----------------------------------------------------------------------
    // Function-typed field descriptor assignability (D-326 / F8)
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeDecl_FunctionFieldDefault_WrongReturnType_EmitsE0001() {
        // fn(): string default on an fn(): int field — structurally incompatible (D-326).
        DiagnosticBag bag = Check("""
            type X {
            cb: fn(): int = () => "wrong"
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
    }

    [Fact]
    public void TypeDecl_FunctionFieldDefault_CorrectSignature_NoError() {
        DiagnosticBag bag = Check("""
            type X {
            cb: fn(): int = () => 42
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void StructConstruction_FunctionFieldValue_WrongSignature_EmitsE0001() {
        // Supplying fn(): string for an fn(): int field — structurally incompatible (D-326).
        DiagnosticBag bag = Check("""
            type X {
            cb: fn(): int
            }
            readonly x := X { cb: () => "wrong" }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
    }

    [Fact]
    public void StructConstruction_FunctionFieldValue_CorrectSignature_NoError() {
        DiagnosticBag bag = Check("""
            type X {
            cb: fn(): int
            }
            readonly x := X { cb: () => 42 }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Struct nominal identity — field values and field defaults
    // (fix/compiler-struct-nominal-identity)
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_FieldValue_WrongNamedStructKind_EmitsE0001() {
        // 'field' is declared Other; supplying a Third construction is a nominal
        // mismatch even though both are the same flat GrobType.Struct tag.
        DiagnosticBag bag = Check("""
            type Other {
            name: string
            }
            type Third {
            label: string
            }
            type Config {
            field: Other
            }
            readonly c := Config { field: Third { label: "x" } }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Equal(10, diag.Range.Start.Line);
        Assert.Equal(31, diag.Range.Start.Column);  // 'Third { label: "x" }' field value
    }

    [Fact]
    public void StructConstruction_FieldValue_MatchingNamedStructKind_NoError() {
        DiagnosticBag bag = Check("""
            type Other {
            name: string
            }
            type Config {
            field: Other
            }
            readonly c := Config { field: Other { name: "x" } }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void TypeDecl_FieldDefault_WrongNamedStructKind_EmitsE0001() {
        // 'field' is declared Other; the default is a Third construction — the same
        // nominal mismatch as the field-value case, but on CheckSingleFieldDefault's path.
        DiagnosticBag bag = Check("""
            type Other {
            name: string
            }
            type Third {
            label: string
            }
            type Config {
            field: Other = Third { label: "x" }
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Equal(8, diag.Range.Start.Line);
    }

    [Fact]
    public void TypeDecl_FieldDefault_MatchingNamedStructKind_NoError() {
        DiagnosticBag bag = Check("""
            type Other {
            name: string
            }
            type Config {
            field: Other = Other { name: "x" }
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Layer-invariant — pathological but parseable inputs never throw
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("type T { }\nreadonly x := T { }")]
    [InlineData("type T { f: int }\nreadonly x := T { f: 1, f: 2 }")]
    [InlineData("type T { f: string }\nreadonly x := T { f: 1 }")]
    [InlineData("readonly x := Unknown { f: 1 }")]
    public void StructConstruction_PathologicalInputs_NeverThrows(string source) {
        // Must complete without throwing — a diagnostic is fine, a crash is not.
        Exception? thrown = Record.Exception(() => Check(source));
        Assert.Null(thrown);
    }
}
