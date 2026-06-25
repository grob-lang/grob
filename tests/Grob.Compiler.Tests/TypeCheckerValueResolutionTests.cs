using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 5 Increment 1 — top-level value-binding type
/// resolution (D-323).
/// </summary>
/// <remarks>
/// Covers the phase-1.5 resolution of top-level value-binding types before pass-2
/// body validation, so forward references from function bodies resolve to the real
/// type rather than <see cref="GrobType.Unknown"/>. Also covers the compile-time
/// E0303 diagnostic for unannotated mutual value-type cycles.
/// </remarks>
public sealed class TypeCheckerValueResolutionTests {
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

    private static IReadOnlyList<IdentifierExpr> CollectIdentifiers(CompilationUnit unit) {
        IdentifierCollector collector = new();
        collector.Visit(unit);
        return collector.Identifiers;
    }

    // -----------------------------------------------------------------------
    // Probe pair (D-323) — forward value-binding read from a function body.
    // These are the primary regression probes for the phase-1.5 resolution.
    // -----------------------------------------------------------------------

    [Fact]
    public void ForwardValueRef_FunctionReturn_Compiles() {
        // Probe 1: 'x' is declared after 'f'; phase 1.5 must resolve x's type to
        // int before pass 2 validates f's body.
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn f(): int {
            return x
            }
            readonly x := 5
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");

        IdentifierExpr xRef = CollectIdentifiers(unit).First(i => i.Name == "x");
        Assert.Equal(GrobType.Int, xRef.ResolvedType);
    }

    [Fact]
    public void ForwardValueRef_WrongType_ReportsE0005_NamingConcreteType() {
        // Probe 2: 'x' is a string; E0005 must name 'string', not 'unknown'.
        DiagnosticBag bag = Check("""
            fn f(): int {
            return x
            }
            readonly x := "hello"
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0005.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(8, diag.Range.Start.Column);
        Assert.Contains("string", diag.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unknown", diag.Message, StringComparison.OrdinalIgnoreCase);
    }

    // -----------------------------------------------------------------------
    // Other typed positions that forward-reference a top-level value binding.
    // -----------------------------------------------------------------------

    [Fact]
    public void ForwardValueRef_InBinaryOp_Resolves() {
        DiagnosticBag bag = Check("""
            fn f(): string {
            return "s" + x
            }
            readonly x := "t"
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void ForwardValueRef_InCallArgument_Resolves() {
        DiagnosticBag bag = Check("""
            fn g(n: int): int {
            return n
            }
            fn f(): int {
            return g(x)
            }
            readonly x := 5
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // E0303 — unannotated value-binding type cycle (compile-time).
    // -----------------------------------------------------------------------

    [Fact]
    public void MutualUnannotatedValueCycle_ReportsE0303() {
        // 'a' and 'b' each reference the other with no type annotation; the type
        // of neither can be inferred without first knowing the other → E0303.
        DiagnosticBag bag = Check("""
            readonly a := b
            readonly b := a
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0303.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Annotated mutual cycle — types resolvable from annotations, no E0303.
    // Surfaces at runtime as E5902 (§19.1), not at compile time.
    // -----------------------------------------------------------------------

    [Fact]
    public void MutualAnnotatedValueCycle_PassesTypeCheck() {
        // 'a: int' and 'b: int' each reference the other with a type annotation.
        // Phase 1.5 resolves each type from its annotation (no value-type dep
        // edge on annotated bindings); the mutual cycle is structurally fine for
        // the type checker and surfaces only as E5902 at runtime.
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            readonly a: int := b
            readonly b: int := a
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");

        IdentifierExpr aRef = CollectIdentifiers(unit).First(i => i.Name == "a");
        IdentifierExpr bRef = CollectIdentifiers(unit).First(i => i.Name == "b");
        Assert.Equal(GrobType.Int, aRef.ResolvedType);
        Assert.Equal(GrobType.Int, bRef.ResolvedType);
    }

    // -----------------------------------------------------------------------
    // Duplicate value binding — the first declaration stays authoritative in
    // pass 1 / phase 1.5, so a forward reader sees the first type and no bogus
    // E0005 cascade precedes the E1102 (PR #92 review).
    // -----------------------------------------------------------------------

    [Fact]
    public void DuplicateValueBinding_ForwardReader_SeesFirstType_NoBogusE0005() {
        // 'foo' is declared twice; the first is int, the second string. A forward
        // reader 'f' expects int. The first provisional must remain authoritative
        // so 'f' resolves cleanly; only the E1102 redeclaration is reported.
        DiagnosticBag bag = Check("""
            fn f(): int {
            return foo
            }
            foo := 1
            foo := "s"
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1102.Code, diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Call arguments are not value-type dependency edges (D-323): a call
    // result's type comes from the callee's declared return type, not its
    // arguments. A cycle that only closes through a call argument is therefore
    // not an E0303; the runtime E5902 path covers it (PR #92 review).
    // -----------------------------------------------------------------------

    [Fact]
    public void CallArgumentCycle_NotTreatedAsTypeDependency_NoE0303() {
        // 'a := id(b)' resolves a from id's declared return type (int), so there
        // is no value-type dependency edge from a to b. 'b := a' depends on a,
        // which is already fixed by id's return type — no compile-time cycle.
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn id(n: int): int {
            return n
            }
            readonly a := id(b)
            readonly b := a
            """);

        Assert.DoesNotContain(bag.Errors, d => d.Code == ErrorCatalog.E0303.Code);

        IdentifierExpr aRef = CollectIdentifiers(unit).First(i => i.Name == "a");
        Assert.Equal(GrobType.Int, aRef.ResolvedType);
    }
}
