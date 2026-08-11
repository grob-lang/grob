using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// TypeChecker tests for <c>map&lt;K, V&gt;?</c> (D-401) — the missing
/// <see cref="GrobType.NullableMap"/> variant, mirroring <see cref="ArrayTypeRefCheckerTests"/>'s
/// coverage of <see cref="GrobType.NullableArray"/> (D-327). D-400 found the gap while sweeping
/// receiver kinds for the <c>?.</c> method-call fix: a nil map receiver could not be constructed
/// from source at all, because <c>map&lt;string, int&gt;? := nil</c> failed to compile.
/// </summary>
public sealed class NullableMapTypeCheckerTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag);
        checker.Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private static void AssertSingleError(DiagnosticBag bag, string code, int line, int column) {
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(code, diag.Code);
        Assert.Equal(line, diag.Range.Start.Line);
        Assert.Equal(column, diag.Range.Start.Column);
    }

    private sealed class CallCollector : AstWalker {
        public List<CallExpr> Nodes { get; } = [];
        public override Unit VisitCall(CallExpr node) {
            Nodes.Add(node);
            return base.VisitCall(node);
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static List<CallExpr> CollectCalls(CompilationUnit unit) {
        var collector = new CallCollector();
        collector.Visit(unit);
        return collector.Nodes;
    }

    private sealed class MemberAccessCollector : AstWalker {
        public List<MemberAccessExpr> Nodes { get; } = [];
        public override Unit VisitMemberAccess(MemberAccessExpr node) {
            Nodes.Add(node);
            return base.VisitMemberAccess(node);
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

    // ------------------------------------------------------------------
    // Annotation → GrobType resolution — the reported case.
    // ------------------------------------------------------------------

    [Fact]
    public void NullableMapAnnotation_AcceptsNil() {
        DiagnosticBag bag = Check("m: map<string, int>? := nil\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void NullableMapAnnotation_AcceptsRealMap() {
        DiagnosticBag bag = Check("""
            m: map<string, int>? := map<string, int>{"k": 1}
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void MapAnnotation_RejectsNil_ProvesNullableMapBehaviourallyDistinct() {
        // map<string, int> (non-nullable) does NOT accept nil — the contrast case that
        // proves NullableMapAnnotation_AcceptsNil exercises real nullable widening, not
        // a permissive fallback.
        DiagnosticBag bag = Check("m: map<string, int> := nil\n");
        AssertSingleError(bag, "E0001", 1, 24);
    }

    [Fact]
    public void NullableMap_Widening_NonNullableMapIsAssignable() {
        DiagnosticBag bag = Check("""
            fn f(source: map<string, int>): void {
            dest: map<string, int>? := source
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Value-type descriptor still resolves through a nullable annotation
    // (D-374's MapTypeDescriptor must not be dropped by the nullable suffix).
    // ------------------------------------------------------------------

    [Fact]
    public void NullableMapAnnotation_ValueMismatch_StillReportsE0001() {
        DiagnosticBag bag = Check("""
            fn f(a: map<string, int>): void {
            c: map<string, string>? := a
            }
            """);
        AssertSingleError(bag, "E0001", 2, 28);
    }

    // ------------------------------------------------------------------
    // The Grob.Http shape — the documented signature this increment unblocks.
    // ------------------------------------------------------------------

    [Fact]
    public void FunctionParameter_NullableMapWithNilDefault_TypeChecks() {
        DiagnosticBag bag = Check("fn f(headers: map<string,string>? = nil): void { }\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // for k, v in — a nullable map is not iterable without a guard, mirroring
    // the nullable-array rule exactly (both fall through the same 'default'
    // arm of ResolveIterationVariableTypes once NullableMap exists).
    // ------------------------------------------------------------------

    [Fact]
    public void ForIn_OnNullableMap_ReportsE0501() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>?): void {
            for k, v in m {
            print(v)
            }
            }
            """);
        AssertSingleError(bag, "E0501", 2, 13);
    }

    [Fact]
    public void ForIn_NilGuardUnwrappedMap_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>?): void {
            for k, v in (m ?? map<string, int>{}) {
            print(v)
            }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // '??' unwrapping — a nullable map unwraps to a non-nullable map usable
    // without further guards.
    // ------------------------------------------------------------------

    [Fact]
    public void NilCoalesce_NullableMapWithFallback_ResolvesToNonNullableMap() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>?): void {
            n: map<string, int> := m ?? map<string, int>{}
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Nil-guard narrowing — 'if m != nil { ... }' narrows m to non-nullable
    // for the extent of the then-block (generic over GrobTypeHelpers, §6).
    // ------------------------------------------------------------------

    [Fact]
    public void NilGuardNarrowing_NullableMap_NarrowsToNonNullableInsideBlock() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn f(m: map<string, int>?): void {
            if (m != nil) {
            n: map<string, int> := m
            print(n)
            }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        Assert.NotNull(unit);
    }

    // ------------------------------------------------------------------
    // Unguarded property access on a nullable map — VisitMemberAccess already
    // rejects generically for every nullable type (E0101).
    // ------------------------------------------------------------------

    [Fact]
    public void UnguardedPropertyAccess_OnNullableMap_ReportsE0101() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>?): void {
            print(m.length)
            }
            """);
        AssertSingleError(bag, "E0101", 2, 7);
    }

    [Fact]
    public void OptionalPropertyAccess_OnNullableMap_ResolvesWidenedFieldType() {
        // D-403: 'm?.length' now dispatches against the underlying non-nullable
        // 'map<string, int>' property table and widens the result to 'int?' — not the
        // old permissive Unknown a bare 'print(...)' argument could not distinguish.
        var (unit, bag) = TypeCheckSource("""
            fn f(m: map<string, int>?): void {
            print(m?.length)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit), m => m.Member == "length");
        Assert.Equal(GrobType.NullableInt, ma.ResolvedFieldType);
    }

    // ------------------------------------------------------------------
    // D-402: the method-call analogue of the property-access guard above.
    // ResolveMemberAccessCall previously matched only the exact non-nullable
    // GrobType.Map tag, so a method call ('.get(...)') did NOT raise E0101 for a
    // nullable map the way '.length' already did — a separate, pre-existing gap
    // (D-401's second finding), closed here alongside the identical array gap.
    // ------------------------------------------------------------------

    [Fact]
    public void UnguardedMethodCallOnNullableMap_ReportsE0101() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>?): void {
            print(m.get("k"))
            }
            """);
        AssertSingleError(bag, "E0101", 2, 7);
    }

    [Fact]
    public void OptionalMethodCallOnNullableMap_ResolvesToNullableValueType() {
        // Load-bearing: 'm?.get("k")' now resolves 'int?' (the value type widened to
        // nullable) instead of the permissive Unknown D-401 reported — closes the gap
        // that made 'y: int? := m?.get("k")' fail E0001 before this fix.
        var (unit, bag) = TypeCheckSource("""
            fn f(m: map<string, int>?): void {
            x := m?.get("k")
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.NullableInt, call.ResolvedReturnType);
    }

    // ------------------------------------------------------------------
    // The TypeName spelling used in diagnostic messages — 'map?', not 'unknown'.
    // ------------------------------------------------------------------

    [Fact]
    public void ForIn_OnNullableMap_MessageSpellsMapQuestionMark() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>?): void {
            for k, v in m {
            print(v)
            }
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Contains("'map?'", diag.Message);
    }
}
