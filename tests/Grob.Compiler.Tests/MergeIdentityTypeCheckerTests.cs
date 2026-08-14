using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-404 — closes the two "seven side-channel gaps" D-403's survey found still open: a
/// ternary and a switch expression perform the identical N-branch structural merge <c>??</c>
/// performs (PR #189, Finding 4), so the same side-channel-identity guard
/// (<c>MergeIdentityMismatch</c>, <c>TypeChecker.Expressions.cs</c>) now runs once after
/// <see cref="TypeChecker.VisitTernary"/>'s/<see cref="TypeChecker.VisitSwitchExpr"/>'s own
/// arm unification, for every kind a merge result's flat <see cref="GrobType"/> tag conflates
/// with a real identity: <c>map</c>, <c>array</c>, <c>fn</c>, a named struct and an anonymous
/// struct. Without the guard, a merge whose branches share the flat tag but disagree on the
/// identity it hides silently keeps the FIRST branch's descriptor — the identity a
/// differently-typed later branch may actually supply at runtime.
/// </summary>
public sealed class MergeIdentityTypeCheckerTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    // =========================================================================
    // Arm survival — matched branches resolve identity through the merge, both forms.
    // =========================================================================

    [Fact]
    public void Ternary_ArrayArms_IndexResolvesElementType_NoErrors() {
        DiagnosticBag bag = Check("""
            cond := true
            y: int := (cond ? [1, 2] : [3, 4])[0]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Switch_ArrayArms_IndexResolvesElementType_NoErrors() {
        DiagnosticBag bag = Check("""
            n := 1
            y: int := (n switch { 1 => [1, 2], _ => [3, 4] })[0]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Ternary_MapArms_IndexResolvesValueType_NoErrors() {
        DiagnosticBag bag = Check("""
            cond := true
            y: int? := (cond ? map<string, int>{} : map<string, int>{})["k"]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Switch_MapArms_IndexResolvesValueType_NoErrors() {
        DiagnosticBag bag = Check("""
            n := 1
            y: int? := (n switch { 1 => map<string, int>{}, _ => map<string, int>{} })["k"]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Ternary_FunctionArms_CallResolvesDeclaredReturnType() {
        DiagnosticBag bag = Check("""
            f: fn(): int := () => 1
            g: fn(): int := () => 2
            cond := true
            y: int := (cond ? f : g)()
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Switch_FunctionArms_CallResolvesDeclaredReturnType() {
        DiagnosticBag bag = Check("""
            f: fn(): int := () => 1
            g: fn(): int := () => 2
            n := 1
            y: int := (n switch { 1 => f, _ => g })()
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Ternary_NamedStructArms_ToStringResolvesViaNamedTypeDispatch() {
        DiagnosticBag bag = Check("""
            g1 := guid.newV4()
            g2 := guid.newV4()
            cond := true
            s: string := (cond ? g1 : g2).toString()
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Switch_NamedStructArms_ToStringResolvesViaNamedTypeDispatch() {
        DiagnosticBag bag = Check("""
            g1 := guid.newV4()
            g2 := guid.newV4()
            n := 1
            s: string := (n switch { 1 => g1, _ => g2 }).toString()
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Ternary_AnonStructArms_FieldAccessResolvesSharedShape() {
        DiagnosticBag bag = Check("""
            cond := true
            y: int := (cond ? #{ x: 1 } : #{ x: 2 }).x
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Switch_AnonStructArms_FieldAccessResolvesSharedShape() {
        DiagnosticBag bag = Check("""
            n := 1
            y: int := (n switch { 1 => #{ x: 1 }, _ => #{ x: 2 } }).x
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // =========================================================================
    // Guard fires — E0002, all five kinds, both forms.
    // =========================================================================

    [Fact]
    public void Ternary_MismatchedArrayElementTypes_RaisesE0002() {
        DiagnosticBag bag = Check("""
            cond := true
            y := (cond ? [1, 2] : ["s"])[0]
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("array element types do not match", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void Switch_MismatchedArrayElementTypes_RaisesE0002() {
        DiagnosticBag bag = Check("""
            n := 1
            y := (n switch { 1 => [1, 2], _ => ["s"] })[0]
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("array element types do not match", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void Ternary_MismatchedMapValueTypes_RaisesE0002() {
        DiagnosticBag bag = Check("""
            cond := true
            y := (cond ? map<string, int>{} : map<string, string>{})["k"]
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("map value types do not match", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void Switch_MismatchedMapValueTypes_RaisesE0002() {
        DiagnosticBag bag = Check("""
            n := 1
            y := (n switch { 1 => map<string, int>{}, _ => map<string, string>{} })["k"]
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("map value types do not match", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void Ternary_MismatchedFunctionSignatures_RaisesE0002() {
        DiagnosticBag bag = Check("""
            f: fn(): int := () => 1
            g: fn(): string := () => "s"
            cond := true
            y := (cond ? f : g)()
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("function signatures do not match", diag.Message);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void Switch_MismatchedFunctionSignatures_RaisesE0002() {
        DiagnosticBag bag = Check("""
            f: fn(): int := () => 1
            g: fn(): string := () => "s"
            n := 1
            y := (n switch { 1 => f, _ => g })()
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("function signatures do not match", diag.Message);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    /// <summary>
    /// Mirrors PR #189's own finding (<c>NilCoalesceDescriptorSymmetryTests.
    /// NamedStruct_MismatchedNilCoalesceOperands_RaisesE0002</c>): <c>date</c> and
    /// <c>guid</c> both carry the flat <see cref="GrobType.Struct"/> tag, so without the
    /// nominal check <c>GetStructTypeName</c> keeps the LEFT branch's name and dispatches
    /// through the wrong named-type method table — proving the guard consults the
    /// correct identity, not just a label.
    /// </summary>
    [Fact]
    public void Ternary_MismatchedNamedStructs_DateAndGuid_RaisesE0002() {
        DiagnosticBag bag = Check("""
            d := date.now()
            g := guid.newV4()
            cond := true
            s := (cond ? d : g).toString()
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("named types do not match", diag.Message);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void Switch_MismatchedNamedStructs_DateAndGuid_RaisesE0002() {
        DiagnosticBag bag = Check("""
            d := date.now()
            g := guid.newV4()
            n := 1
            s := (n switch { 1 => d, _ => g }).toString()
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("named types do not match", diag.Message);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void Ternary_MismatchedAnonStructShapes_RaisesE0002() {
        DiagnosticBag bag = Check("""
            cond := true
            y := (cond ? #{ x: 1 } : #{ y: 2 }).x
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("struct shapes do not match", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void Switch_MismatchedAnonStructShapes_RaisesE0002() {
        DiagnosticBag bag = Check("""
            n := 1
            y := (n switch { 1 => #{ x: 1 }, _ => #{ y: 2 } }).x
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Contains("struct shapes do not match", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    // =========================================================================
    // Message wording — pinned once, exactly, for the decisions-log entry.
    // =========================================================================

    [Fact]
    public void Ternary_MismatchedArrayElementTypes_MessageWording() {
        DiagnosticBag bag = Check("""
            cond := true
            y := (cond ? [1, 2] : ["s"])[0]
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("Ternary arms must produce the same type: array element types do not match.", diag.Message);
    }

    [Fact]
    public void Switch_MismatchedArrayElementTypes_MessageWording() {
        DiagnosticBag bag = Check("""
            n := 1
            y := (n switch { 1 => [1, 2], _ => ["s"] })[0]
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("Switch arms must produce the same type: array element types do not match.", diag.Message);
    }

    // =========================================================================
    // Permissiveness preserved — an Unknown-element branch stays permissive.
    // =========================================================================

    [Fact]
    public void Ternary_EmptyArrayLiteralArm_StaysPermissive_NoErrors() {
        DiagnosticBag bag = Check("""
            cond := true
            y := cond ? [] : [1, 2]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Switch_EmptyArrayLiteralArm_StaysPermissive_NoErrors() {
        DiagnosticBag bag = Check("""
            n := 1
            y := n switch { 1 => [], _ => [1, 2] }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // =========================================================================
    // N-branch behaviour — a switch with 3+ arms reports once, not per-pair; a nested
    // merge reports at the innermost point only, no cascade.
    // =========================================================================

    [Fact]
    public void Switch_ThreeArms_OneMismatchedPair_ReportsOnce() {
        // Arms 1 and 2 mismatch (int[] vs string[]); arms 2 and 3 also mismatch
        // (string[] vs int[]) — MergeIdentityMismatch must still report only once.
        DiagnosticBag bag = Check("""
            n := 1
            y := n switch { 1 => [1, 2], 2 => ["s"], _ => [3, 4] }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    [Fact]
    public void Switch_NestedTernaryInArm_ReportsAtInnermostPoint_NoCascade() {
        DiagnosticBag bag = Check("""
            n := 1
            cond := true
            y := n switch { 1 => (cond ? [1, 2] : ["s"]), _ => [3, 4] }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(23, diag.Range.Start.Column);
    }

    [Fact]
    public void Ternary_NestedSwitchInArm_ReportsAtInnermostPoint_NoCascade() {
        DiagnosticBag bag = Check("""
            n := 1
            cond := true
            y := cond ? (n switch { 1 => [1, 2], _ => ["s"] }) : [3, 4]
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(14, diag.Range.Start.Column);
    }

    // =========================================================================
    // Bonus-fix tests (D-404) — the NilCoalesce arm added to the three previously
    // unpinned helpers now resolves identity through '??' at these two real call
    // sites, where it previously fell to null.
    // =========================================================================

    [Fact]
    public void AnonStructFieldInitialiser_NilCoalesce_ResolvesFieldStructIdentity() {
        // Before the bonus fix: GetFieldValueStructTypeName had no BinaryExpr{NilCoalesce}
        // arm, so field 'x' recorded no NamedTypeName even though 'a ?? b' resolves to
        // Foo — 's.x.n' then silently degraded to Unknown instead of int.
        DiagnosticBag bag = Check("""
            type Foo {
                n: int
            }
            a := Foo { n: 1 }
            b := Foo { n: 2 }
            s := #{ x: a ?? b }
            y: int := s.x.n
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void ThrowStatement_NilCoalesceOperand_ResolvesExceptionIdentity() {
        // Before the bonus fix: GetFieldValueStructTypeName's throw-operand consultation
        // (VisitThrow) had no BinaryExpr{NilCoalesce} arm, so 'throw a ?? b' was
        // misreported as E0014 even though both branches are IoError.
        DiagnosticBag bag = Check("""
            a: IoError? := nil
            b := IoError { message: "x" }
            throw a ?? b
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // =========================================================================
    // D-406 — a switch arm the parser drops during local recovery is simply absent
    // from SwitchExprNode.Arms, so CheckMergeIdentity (run once, after the fold,
    // over the SURVIVING arms only) needs no change of its own: recovery neither
    // suppresses a real mismatch among the arms that remain nor fabricates a
    // spurious one. Both directions are pinned explicitly rather than assumed.
    // =========================================================================

    [Fact]
    public void Switch_RecoveredArm_DoesNotSuppressGenuineMismatchAmongSurvivingArms_RaisesE0002() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            n := 1
            y := (n switch { 1 10, 2 => [1, 2], _ => ["s"] })[0]
            """);
        Assert.Equal(2, bag.Errors.Count());
        Diagnostic parseDiag = Assert.Single(bag.Errors, e => e.Code == "E2001");
        // The '10' that should have been preceded by '=>' — line 2, column 20.
        Assert.Equal(2, parseDiag.Range.Start.Line);
        Assert.Equal(20, parseDiag.Range.Start.Column);
        Diagnostic mergeDiag = Assert.Single(bag.Errors, e => e.Code == "E0002");
        Assert.Contains("array element types do not match", mergeDiag.Message);
        Assert.Equal(2, mergeDiag.Range.Start.Line);
        Assert.Equal(7, mergeDiag.Range.Start.Column);
        Assert.NotNull(unit);
    }

    [Fact]
    public void Switch_RecoveredArm_SurvivingArmsAgree_DoesNotSpuriouslyRaiseE0002() {
        DiagnosticBag bag = Check("""
            n := 1
            y := (n switch { 1 10, 2 => [1, 2], _ => [3, 4] })[0]
            """);
        Diagnostic onlyDiag = Assert.Single(bag.Errors);
        Assert.Equal("E2001", onlyDiag.Code);
        // The '10' that should have been preceded by '=>' — line 2, column 20.
        Assert.Equal(2, onlyDiag.Range.Start.Line);
        Assert.Equal(20, onlyDiag.Range.Start.Column);
        Assert.DoesNotContain(bag.Errors, e => e.Code == "E0002");
    }
}
