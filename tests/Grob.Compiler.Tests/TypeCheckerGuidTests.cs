using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 8 Increment D — the <c>guid</c> primitive type. Covers
/// signature-position type identity (distinct from <c>string</c>), the namespace-call
/// surface (D-342), the two-level <c>guid.namespaces.dns</c> chain, instance member
/// dispatch (<c>version</c>/<c>isEmpty</c>/<c>toString</c> family), and the compile-time
/// literal-validation seam (populated fully once the new error code lands).
/// </summary>
public sealed class TypeCheckerGuidTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private static string FormatErrors(DiagnosticBag bag) =>
        string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"));

    // -----------------------------------------------------------------------
    // Signature-position type identity — guid is Struct, distinct from string.
    // -----------------------------------------------------------------------

    [Fact]
    public void GuidParameter_Annotation_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            fn describe(id: guid): guid {
                return id
            }
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void GuidField_Annotation_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            type Resource {
                id: guid
            }
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void GuidParameter_AssignedString_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""
            fn take(id: guid): void {}
            take("not-a-guid")
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Nominal identity — guid is not interchangeable with an unrelated named
    // struct, even though both share the flat GrobType.Struct tag (CodeRabbit
    // review, PR #133).
    // -----------------------------------------------------------------------

    [Fact]
    public void GuidParameter_AssignedUnrelatedStruct_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""
            type Config {
                host: string
            }
            fn take(id: guid): void {}
            c := Config { host: "example.com" }
            take(c)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(6, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    [Fact]
    public void StructParameter_AssignedGuid_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""
            type Config {
                host: string
            }
            fn take(c: Config): void {}
            id := guid.newV4()
            take(id)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(6, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    [Fact]
    public void GuidParameter_AssignedGuid_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            fn take(id: guid): void {}
            take(guid.newV4())
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void GuidValue_ComparedToString_ReportsSingleE0002() {
        DiagnosticBag bag = Check("""
            id := guid.newV4()
            readonly r := id == "not-a-guid"
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void GuidValue_ComparedToGuid_ResolvesToBool_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            a := guid.newV4()
            b := guid.newV4()
            readonly r := a == b
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // Namespace-call surface (D-342) — guid.newV4()/newV7()/newV5()/parse()/tryParse().
    // -----------------------------------------------------------------------

    [Fact]
    public void NewV4_Call_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly id := guid.newV4()");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void NewV7_Call_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly id := guid.newV7()");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void NewV5_VariadicCall_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly id := guid.newV5(guid.namespaces.url, "a", "b", "c")""");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void NewV5_NoNameSegments_ReportsSingleE0003() {
        DiagnosticBag bag = Check("""readonly id := guid.newV5(guid.namespaces.url)""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(16, diag.Range.Start.Column);
    }

    [Fact]
    public void NewV5_UnrelatedStructAsNamespace_ReportsSingleE0004() {
        // The namespace argument must itself be a guid — an unrelated struct sharing the
        // flat GrobType.Struct tag must not pass (CodeRabbit review, PR #133).
        DiagnosticBag bag = Check("""
            type Config {
                host: string
            }
            c := Config { host: "example.com" }
            readonly id := guid.newV5(c, "a")
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(27, diag.Range.Start.Column);
    }

    [Fact]
    public void Parse_ValidCall_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly id := guid.parse("550e8400-e29b-41d4-a716-446655440000")""");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // Compile-time literal validation (D-149) — guid.parse("<literal>").
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_MalformedStringLiteral_ReportsSingleE0601WithLocation() {
        // CodeRabbit review, PR #133: previously split across two tests (code-only and
        // location-only), leaving neither test to verify the full diagnostic contract.
        DiagnosticBag bag = Check("""readonly id := guid.parse("not-a-guid")""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0601.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(27, diag.Range.Start.Column);
    }

    [Fact]
    public void Parse_NonLiteralArgument_MalformedAtRuntime_IsNotACompileError() {
        // The value ("not-a-guid") is only known at runtime through the variable — the
        // literal check must not fire, and the call stays on the ordinary runtime path.
        DiagnosticBag bag = Check("""
            s := "not-a-guid"
            readonly id := guid.parse(s)
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Parse_ValidStringLiteral_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly id := guid.parse("550E8400-E29B-41D4-A716-446655440000")""");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Parse_MalformedRawStringLiteral_ReportsSingleE0601() {
        // Raw (backtick) strings are also compile-time literals — CodeRabbit review,
        // PR #133: the check only handled the interpolated-string form.
        DiagnosticBag bag = Check("readonly id := guid.parse(`not-a-guid`)");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0601.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(27, diag.Range.Start.Column);
    }

    [Fact]
    public void Parse_ValidRawStringLiteral_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly id := guid.parse(`550e8400-e29b-41d4-a716-446655440000`)");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void TryParse_Call_ResolvesToNullableStruct() {
        var (unit, bag) = TypeCheckSource("""readonly id := guid.tryParse("not-a-guid-and-not-a-literal-check")""");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        var ma = new MemberAccessCollector();
        ma.Visit(unit);
        MemberAccessExpr node = Assert.Single(ma.Nodes);
        Assert.Equal(GrobType.NullableStruct, node.ResolvedFieldType);
    }

    [Fact]
    public void Empty_Constant_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly id := guid.empty");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // The two-level namespace chain — guid.namespaces.dns/url/oid.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("dns")]
    [InlineData("url")]
    [InlineData("oid")]
    public void Namespaces_KnownMember_ResolvesToStruct_NoDiagnostics(string member) {
        DiagnosticBag bag = Check($"readonly ns := guid.namespaces.{member}");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Namespaces_UnknownMember_ReportsSingleE1003() {
        DiagnosticBag bag = Check("readonly ns := guid.namespaces.nope");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(16, diag.Range.Start.Column);
    }

    [Fact]
    public void NamespacesBare_ReportsSingleE1003() {
        DiagnosticBag bag = Check("readonly ns := guid.namespaces");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(16, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Instance member dispatch — version/isEmpty (properties), toString family (methods).
    // -----------------------------------------------------------------------

    [Fact]
    public void Version_Property_ResolvesToInt_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            id := guid.newV4()
            readonly v := id.version
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void IsEmpty_Property_ResolvesToBool_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            id := guid.newV4()
            readonly e := id.isEmpty
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Theory]
    [InlineData("toString")]
    [InlineData("toUpperString")]
    [InlineData("toCompactString")]
    public void ToStringFamily_Call_ResolvesToString_NoDiagnostics(string method) {
        DiagnosticBag bag = Check($"""
            id := guid.newV4()
            readonly s := id.{method}()
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void ToString_WithArgument_ReportsSingleE0003() {
        DiagnosticBag bag = Check("""
            id := guid.newV4()
            readonly s := id.toString("x")
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void UnknownMethod_Call_ReportsSingleE1002() {
        DiagnosticBag bag = Check("""
            id := guid.newV4()
            readonly s := id.nope()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void UnknownBareMember_ReportsSingleE1002() {
        // A bare (uncalled) unknown member must not survive type checking as Unknown —
        // it would otherwise crash the VM with an internal exception instead of failing
        // at compile time (CodeRabbit review, PR #133).
        DiagnosticBag bag = Check("""
            id := guid.newV4()
            readonly s := id.nope
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void GuidParameter_InstanceMemberAccess_ResolvesWithNoDiagnostics() {
        // A guid-typed function PARAMETER (not a `:=`-inferred local) must resolve its
        // instance members the same way — exercises ResolveSignatureType's guid branch
        // feeding GetStructTypeName via the parameter symbol path.
        DiagnosticBag bag = Check("""
            fn describe(id: guid): string {
                return id.toString()
            }
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // §3.1.1 — every identifier node carries a non-null ResolvedType/Declaration.
    // -----------------------------------------------------------------------

    [Fact]
    public void GuidNamespaceReceiver_AnnotatesWithoutErroring() {
        var (unit, _) = TypeCheckSource("readonly id := guid.newV4()");

        var identifiers = new IdentifierCollector();
        identifiers.Visit(unit);
        IdentifierExpr guidRef = identifiers.Identifiers.First(i => i.Name == "guid");
        Assert.NotNull(guidRef.Declaration);
        Assert.IsType<NamespaceDecl>(guidRef.Declaration);
    }

    [Fact]
    public void GuidAsValue_ReportsSingleE1004() {
        // 'guid' bare (no member access) is a namespace in value position, same as 'math'.
        DiagnosticBag bag = Check("readonly x := guid");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Regression — existing array/struct arms unaffected by the new guid arms.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayHigherOrderMethod_Unchanged_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            arr := [1, 2, 3]
            arr.select(x => x)
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void UserStructFieldAccess_Unchanged_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            type Config {
                host: string
            }
            readonly c := Config { host: "example.com" }
            readonly h := c.host
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // Test helpers.
    // -----------------------------------------------------------------------

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
}
