using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 8 Increment A — module-namespace member-access
/// dispatch precedence (D-342). Covers the three call sites that must agree on one
/// rule: a namespace receiver is peeked (never visited) so a namespace in member
/// position never trips the namespace-as-value diagnostic on its own receiver, and
/// falls through unchanged to array higher-order methods and struct field access for
/// every non-namespace receiver.
/// </summary>
public sealed class TypeCheckerNamespaceAccessTests {
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

    private static IReadOnlyList<IdentifierExpr> CollectIdentifiers(CompilationUnit unit) {
        IdentifierCollector collector = new();
        collector.Visit(unit);
        return collector.Identifiers;
    }

    private static IReadOnlyList<MemberAccessExpr> CollectMemberAccesses(CompilationUnit unit) {
        MemberAccessCollector collector = new();
        collector.Visit(unit);
        return collector.Nodes;
    }

    // -----------------------------------------------------------------------
    // Constant member — math.pi (VisitMemberAccess).
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceConstant_Pi_ResolvesToFloat_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("readonly x := math.pi");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Float, ma.ResolvedFieldType);
    }

    [Fact]
    public void NamespaceConstant_Pi_AnnotatesReceiverWithoutErroring() {
        // §3.1.1: the receiver 'math' is peeked, not visited, so it must still carry a
        // non-null Declaration (the NamespaceDecl) rather than tripping E1004 on itself.
        var (unit, _) = TypeCheckSource("readonly x := math.pi");

        IdentifierExpr mathRef = CollectIdentifiers(unit).First(i => i.Name == "math");
        Assert.NotNull(mathRef.Declaration);
        Assert.IsType<NamespaceDecl>(mathRef.Declaration);
    }

    // -----------------------------------------------------------------------
    // Native member call — math.sqrt(...) (VisitCall).
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceNative_SqrtValidArg_ResolvesToFloat_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("readonly x := math.sqrt(9.0)");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Float, ma.ResolvedFieldType);
    }

    [Fact]
    public void NamespaceNative_SqrtIntArg_WidensToFloat_NoDiagnostics() {
        // int → float is the one implicit widening; a native taking float accepts an int.
        DiagnosticBag bag = Check("readonly x := math.sqrt(9)");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void NamespaceNative_SqrtWrongArgType_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""readonly x := math.sqrt("not a float")""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(25, diag.Range.Start.Column);
    }

    [Fact]
    public void NamespaceNative_SqrtWrongArity_ReportsSingleE0003() {
        DiagnosticBag bag = Check("readonly x := math.sqrt(1.0, 2.0)");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void NamespaceNative_SqrtTooFewArgs_ReportsSingleE0003() {
        DiagnosticBag bag = Check("readonly x := math.sqrt()");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void NamespaceNative_Bare_ResolvesToFunction_NoDiagnostics() {
        // A native accessed without a call is a callable member; typed as Function,
        // mirroring how a bare user-fn reference resolves. First-class use is not wired
        // for emission in this increment — flagged as a design gap.
        var (unit, bag) = TypeCheckSource("readonly f := math.sqrt");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Function, ma.ResolvedFieldType);
    }

    // -----------------------------------------------------------------------
    // Unknown member — math.nope (E1003), both call and bare positions.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceUnknownMember_Called_ReportsSingleE1003() {
        DiagnosticBag bag = Check("readonly x := math.nope()");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void NamespaceUnknownMember_Bare_ReportsSingleE1003() {
        DiagnosticBag bag = Check("readonly x := math.nope");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void NamespaceUnknownMember_Bare_ReceiverStillResolvedToNamespace() {
        // §3.1.1: even on the E1003 path the receiver is a valid namespace, so its
        // Declaration is the NamespaceDecl (never null, never UnresolvedDecl).
        var (unit, _) = TypeCheckSource("readonly x := math.nope");

        IdentifierExpr mathRef = CollectIdentifiers(unit).First(i => i.Name == "math");
        Assert.NotNull(mathRef.Declaration);
        Assert.IsType<NamespaceDecl>(mathRef.Declaration);
    }

    // -----------------------------------------------------------------------
    // Namespace in value position — E1004 (VisitIdentifier).
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceAsValue_Binding_ReportsSingleE1004() {
        DiagnosticBag bag = Check("readonly x := math");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void NamespaceAsValue_CallArgument_ReportsSingleE1004() {
        DiagnosticBag bag = Check("print(math)");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void NamespaceAsValue_ErrorPath_SetsUnresolvedDeclAndErrorType() {
        // §3.1.1 invariant on the new E1004 path, mirroring the E2102 (type-as-value) arm.
        var (unit, _) = TypeCheckSource("print(math)");

        IdentifierExpr mathRef = CollectIdentifiers(unit).First(i => i.Name == "math");
        Assert.Same(UnresolvedDecl.Instance, mathRef.Declaration);
        Assert.Equal(GrobType.Error, mathRef.ResolvedType);
    }

    // -----------------------------------------------------------------------
    // Fall-through regressions — the existing arms must resolve unchanged.
    // -----------------------------------------------------------------------

    [Fact]
    public void StructFieldAccess_Unchanged_ResolvesWithNoNamespaceInterference() {
        var (unit, bag) = TypeCheckSource("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "example.com", port: 8080 }
            readonly h := c.host
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal("host", ma.Member);
        Assert.Equal(GrobType.String, ma.ResolvedFieldType);
    }

    [Fact]
    public void ArrayHigherOrderMethod_Unchanged_ResolvesWithNoNamespaceInterference() {
        DiagnosticBag bag = Check("""
            arr := [1, 2, 3]
            arr.select(x => x)
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Regression: lexical shadowing of a namespace name (PR #127 review —
    // CodeRabbit). TryAnnotateNamespaceReceiver/VisitMemberAccess's fast path
    // must resolve the receiver identifier through LookupSymbol (respecting
    // scope) rather than the bare NamespaceRegistry.IsNamespace(id.Name) string
    // check, so a local parameter named 'math' correctly shadows the global
    // namespace instead of always winning.
    // -----------------------------------------------------------------------

    [Fact]
    public void LocalParameterNamedMath_ShadowsNamespace_ResolvesStructFieldNotNamespaceMember() {
        var (unit, bag) = TypeCheckSource("""
            type Config {
                pi: string
            }
            fn describe(math: Config): string {
                return math.pi
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        // The receiver is the Config-typed parameter, not the math namespace: the
        // field is Config.pi (string), not the namespace constant math.pi (float).
        Assert.Equal(GrobType.String, ma.ResolvedFieldType);
    }

    [Fact]
    public void LocalParameterNamedMath_ShadowsNamespace_UnknownFieldReportsE1002NotE1003() {
        // If the namespace path incorrectly won, an unknown member would be E1003
        // ("undefined module"); the correct struct-field path reports E1002
        // ("undefined member") instead — the two codes distinguish which arm ran.
        DiagnosticBag bag = Check("""
            type Config {
                host: string
            }
            fn describe(math: Config): string {
                return math.nope
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
    }

    // -----------------------------------------------------------------------
    // Cross-compile object identity — allocation-regression guard (D-338's
    // RegisterExceptionHierarchy shape, applied here to RegisterNamespaces).
    // Check() runs RegisterNamespaces unconditionally on every compile with
    // content identical each time, so a fresh NamespaceDecl/Symbol pair per
    // compile is a fixed per-compile allocation cost regardless of whether the
    // source references any namespace — this regressed the compile benchmarks
    // (Compile_TwoExpressions/Compile_TenPrints, GitHub Actions run 29392344084).
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceDecl_AcrossTwoCompiles_IsSameCachedInstance() {
        var (unitA, _) = TypeCheckSource("readonly x := math.pi");
        var (unitB, _) = TypeCheckSource("readonly y := math.pi");

        IdentifierExpr mathA = CollectIdentifiers(unitA).First(i => i.Name == "math");
        IdentifierExpr mathB = CollectIdentifiers(unitB).First(i => i.Name == "math");

        Assert.Same(mathA.Declaration, mathB.Declaration);
    }
}
