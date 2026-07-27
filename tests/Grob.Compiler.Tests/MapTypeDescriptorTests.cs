using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// TypeChecker tests for <c>MapTypeDescriptor</c> (Sprint 9 Increment C0b-1) — the map value-
/// type descriptor, mirroring <c>ArrayTypeDescriptor</c> (D-351). Closes the F5-1 build-status
/// note: <c>map&lt;K, V&gt;</c>'s <c>TypeArguments</c> are now consulted for <c>V</c>, the
/// indexer types <c>m[key]</c> as nullable <c>V?</c>, and <c>for k, v in m</c> binds <c>v</c>
/// as the map's real value type rather than <c>Unknown</c>.
/// <para>
/// Written when no map literal existed in the v1 parser, so every map value in most of this
/// file is threaded through a <c>fn</c> parameter annotation (D-350/D-373's established
/// convention for exercising map typing without construction syntax). D-376 landed map-literal
/// construction; the final section below re-drives a couple of these same scenarios through
/// real parsed literal source instead of a hand-built annotation, per D-374's own noted
/// follow-on (dedicated literal-specific coverage lives in <c>TypeCheckerMapLiteralTests</c>).
/// </para>
/// </summary>
public sealed class MapTypeDescriptorTests {
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

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) { Identifiers.Add(node); return default; }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static IdentifierExpr FindIdentifier(CompilationUnit unit, string name) {
        IdentifierCollector collector = new();
        collector.Visit(unit);
        return collector.Identifiers.First(id => id.Name == name);
    }

    // ------------------------------------------------------------------
    // Indexer typing — m[key] resolves to nullable V, not Unknown.
    // ------------------------------------------------------------------

    [Fact]
    public void IndexRead_OnMapStringInt_ResolvesToNullableInt() {
        // Assigning m["k"] (int?) to a non-nullable int annotation is E0104 — proving
        // VisitIndex no longer returns unconditional Unknown for a known map.
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>): void {
            n: int := m["k"]
            }
            """);
        AssertSingleError(bag, "E0104", 2, 11);
    }

    [Fact]
    public void IndexRead_OnMapStringInt_AssignableToNullableInt_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>): void {
            n: int? := m["k"]
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void IndexRead_OnMapStringString_ResolvesToNullableString() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, string>): void {
            s: string := m["k"]
            }
            """);
        AssertSingleError(bag, "E0104", 2, 14);
    }

    // ------------------------------------------------------------------
    // Nested map<string, T[]> — the value's own ArrayTypeDescriptor.
    // ------------------------------------------------------------------

    [Fact]
    public void IndexRead_OnMapStringIntArray_ResolvesToNullableIntArray() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int[]>): void {
            xs: int[] := m["k"]
            }
            """);
        AssertSingleError(bag, "E0104", 2, 14);
    }

    [Fact]
    public void IndexRead_OnMapStringIntArray_AssignableToNullableIntArrayAnnotation_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int[]>): void {
            xs: int[]? := m["k"]
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void IndexRead_OnMapStringIntArray_WrongNestedElementType_ReportsE0001() {
        // The nested element type (int[] vs string[]) is a distinct mismatch from the
        // outer nullable widening — this proves ValueArrayDescriptor.ElementKind threads
        // correctly, not merely the outer Array/NullableArray flat tag. Both sides are
        // nullable, so PickAssignabilityError yields E0001 (not E0104) — the test name now
        // matches the asserted code.
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int[]>): void {
            xs: string[]? := m["k"]
            }
            """);
        AssertSingleError(bag, "E0001", 2, 18);
    }

    // ------------------------------------------------------------------
    // for k, v in m — the F5-1 defect: v bound as Unknown, now V.
    // ------------------------------------------------------------------

    [Fact]
    public void ForInMap_BindsValueAsMapValueType_NotUnknown() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn iter(m: map<string, string>): void {
            for k, v in m {
            print(v)
            }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        Assert.Equal(GrobType.String, FindIdentifier(unit, "v").ResolvedType);
    }

    [Fact]
    public void ForInMap_BindsKeyAsString_ValueAsIntNotUnknown() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn iter(m: map<string, int>): void {
            for k, v in m {
            print(k)
            print(v)
            }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        Assert.Equal(GrobType.String, FindIdentifier(unit, "k").ResolvedType);
        Assert.Equal(GrobType.Int, FindIdentifier(unit, "v").ResolvedType);
    }

    [Fact]
    public void ForInMap_ValueUsedArithmetically_NoDiagnostics() {
        // v binds as plain int (not int?) — iteration only yields present entries, so no
        // nullable-misuse diagnostic on ordinary arithmetic use.
        DiagnosticBag bag = Check("""
            fn iter(m: map<string, int>): void {
            total := 0
            for k, v in m {
            total = total + v
            }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // D-359 continuity — m[k] += 1 / m[k]++ stay legal on a known map.
    // ------------------------------------------------------------------

    [Fact]
    public void MapIndexCompoundAssign_OnMapStringInt_StaysLegal() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>): void {
            m["k"] += 1
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void MapIndexIncrement_OnMapStringInt_StaysLegal() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>): void {
            m["k"]++
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void MapIndexCompoundAssign_OnMapStringFloat_StaysLegal() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, float>): void {
            m["k"] += 1.5
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void MapIndexIncrement_OnMapStringFloat_ReportsE0002() {
        // The failure-path counterpart to MapIndexIncrement_OnMapStringInt_StaysLegal: the
        // D-359 unwrap makes 'm["k"]++' check against the map's real V (float here), not
        // permissively pass through Unknown as it did before this increment's nullable
        // typing landed — float has no '++' operator (mirrors the identifier-target rule),
        // so this now correctly rejects where it was previously silently permissive.
        DiagnosticBag bag = Check("""
            fn f(m: map<string, float>): void {
            m["k"]++
            }
            """);
        AssertSingleError(bag, "E0002", 2, 1);
    }

    [Fact]
    public void MapIndexCompoundAssign_WrongOperandType_StillRejected() {
        // The D-359 unwrap only relaxes the compound-assignment gate to accept the map's
        // real V, not to make it permissive outright — a genuine mismatch is still caught.
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>): void {
            m["k"] += "x"
            }
            """);
        AssertSingleError(bag, "E0002", 2, 1);
    }

    // ------------------------------------------------------------------
    // Plain index-store: m[k] = v — V is assignable to V? by ordinary widening.
    // ------------------------------------------------------------------

    [Fact]
    public void MapIndexWrite_OnMapStringInt_StaysLegal() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>): void {
            m["k"] = 5
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void MapIndexWrite_WrongType_ReportsE0001() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>): void {
            m["k"] = "x"
            }
            """);
        AssertSingleError(bag, "E0001", 2, 10);
    }

    // ------------------------------------------------------------------
    // Write-side value identity — m[k] = v enforces the map value's real
    // nested/nominal identity, mirroring the already-tested read side.
    // ------------------------------------------------------------------

    [Fact]
    public void MapIndexWrite_OnMapStringIntArray_WrongNestedElementType_ReportsE0001() {
        // Write-side mirror of IndexRead_OnMapStringIntArray_WrongNestedElementType: the
        // map value's own ArrayTypeDescriptor (int[]) must reject a string[] RHS, the same
        // way the read side does — otherwise value identity is enforced on read yet silently
        // dropped on write.
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int[]>, bad: string[]): void {
            m["k"] = bad
            }
            """);
        AssertSingleError(bag, "E0001", 2, 10);
    }

    [Fact]
    public void MapIndexWrite_OnMapStringStruct_WrongStructType_ReportsE0001() {
        // Write-side nominal identity: a map<string, A> value rejects a B RHS via the map
        // value's ValueNamedTypeName, mirroring the array-element struct-nominal check.
        DiagnosticBag bag = Check("""
            type A { x: int }
            type B { x: int }
            fn f(m: map<string, A>, b: B): void {
            m["k"] = b
            }
            """);
        AssertSingleError(bag, "E0001", 4, 10);
    }

    [Fact]
    public void MapIndexWrite_OnMapStringIntArray_CorrectNestedElementType_NoDiagnostic() {
        // Guard against over-rejection: a value-identical nested array binds cleanly.
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int[]>, good: int[]): void {
            m["k"] = good
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Annotated ':=' binding value identity — a map<string, V1> is not
    // assignable to a map<string, V2> annotation when V1 != V2.
    // ------------------------------------------------------------------

    [Fact]
    public void AnnotatedBinding_MapValueMismatch_ReportsE0001() {
        // The flat Map tag matches, but the value identity (int vs string) must still be
        // checked — mirroring the array-element gate already in ResolveBindingFull.
        DiagnosticBag bag = Check("""
            fn f(a: map<string, int>): void {
            c: map<string, string> := a
            }
            """);
        AssertSingleError(bag, "E0001", 2, 27);
    }

    [Fact]
    public void AnnotatedBinding_MapNestedArrayValueMismatch_ReportsE0001() {
        DiagnosticBag bag = Check("""
            fn f(a: map<string, int[]>): void {
            c: map<string, string[]> := a
            }
            """);
        AssertSingleError(bag, "E0001", 2, 29);
    }

    [Fact]
    public void AnnotatedBinding_MapStructValueMismatch_ReportsE0001() {
        // Covers MapValueAssignable's nominal branch: a map<string, A> is not assignable to
        // a map<string, B> annotation even though both values are flat Struct.
        DiagnosticBag bag = Check("""
            type A { x: int }
            type B { x: int }
            fn f(a: map<string, A>): void {
            c: map<string, B> := a
            }
            """);
        AssertSingleError(bag, "E0001", 4, 22);
    }

    [Fact]
    public void AnnotatedBinding_MatchingMapValue_NoDiagnostic() {
        // Guard against over-rejection: a value-identical map binds cleanly.
        DiagnosticBag bag = Check("""
            fn f(a: map<string, int>): void {
            c: map<string, int> := a
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Inferred (':=') binding from an identifier carrying a MapTypeDescriptor.
    // ------------------------------------------------------------------

    [Fact]
    public void InferredBinding_FromMapParameter_CarriesMapDescriptor() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>): void {
            n2 := m
            x: int := n2["k"]
            }
            """);
        AssertSingleError(bag, "E0104", 3, 11);
    }

    // ------------------------------------------------------------------
    // Non-string key type argument — currently permissive; escalated, not silently built.
    // ------------------------------------------------------------------

    [Fact]
    public void NonStringKeyTypeArgument_ValueStillResolves_NoDiagnostic() {
        // v1 keys are string-only; a non-string first type argument has no existing
        // diagnostic home (checked against grob-error-codes.md — E0401/E0402 are unused,
        // reserved for D-080's user-declarable-generics model, a different mechanism).
        // Escalated to the maintainer rather than silently accepted forever or given an
        // invented code — locks today's actual (permissive) behaviour: K is not validated,
        // V still resolves correctly (string, wrapped nullable by the indexer) regardless.
        DiagnosticBag bag = Check("""
            fn f(m: map<int, string>): void {
            s: string? := m["k"]
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Bare 'map' annotation (no type arguments) — stays permissive.
    // ------------------------------------------------------------------

    [Fact]
    public void BareMapAnnotation_IndexRead_StaysUnknown_NoDiagnostic() {
        // No annotation ('=:'-inferred) so an Unknown-typed initialiser is accepted
        // unconditionally — proving the bare-map (no type arguments) case still carries no
        // MapTypeDescriptor and VisitIndex still falls back to Unknown, exactly as before
        // this increment.
        DiagnosticBag bag = Check("""
            fn f(m: map): void {
            s := m["k"]
            n := m["k"]
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // D-376 follow-on: the same indexer/for-in scenarios above, now driven through a
    // real map<K, V>{...} literal rather than a fn-parameter annotation.
    // ------------------------------------------------------------------

    [Fact]
    public void IndexRead_OnMapLiteralStringInt_ResolvesToNullableInt() {
        DiagnosticBag bag = Check("""
            m := map<string, int>{"k": 1}
            n: int := m["k"]
            """);
        AssertSingleError(bag, "E0104", 2, 11);
    }

    [Fact]
    public void ForInMapLiteral_BindsKeyAsString_ValueAsIntNotUnknown() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            m := map<string, int>{"k": 1}
            for k, v in m {
            print(k)
            print(v)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        Assert.Equal(GrobType.String, FindIdentifier(unit, "k").ResolvedType);
        Assert.Equal(GrobType.Int, FindIdentifier(unit, "v").ResolvedType);
    }
}
