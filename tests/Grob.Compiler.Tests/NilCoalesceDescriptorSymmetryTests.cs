using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-402/D-403 — the <c>BinaryExpr { Operator: NilCoalesce }</c> arm every
/// side-channel type-resolution helper (<c>ArrayDescriptorOf</c>, <c>MapDescriptorOf</c>,
/// <c>ExpressionDescriptor</c>, <c>GetStructTypeName</c>) must carry so a <c>??</c>-unwrapped
/// value keeps its structural identity instead of degrading to <see cref="GrobType.Unknown"/>.
/// D-401 added the arm to <c>MapDescriptorOf</c> only (its own acceptance test could not pass
/// otherwise); D-402 restored it on <c>ArrayDescriptorOf</c>; D-403 restores it on the
/// remaining two helpers D-402 found and deliberately left open. All four are asserted
/// together here, in one file, so the restored symmetry is visible in a single place rather
/// than scattered across each helper's own dedicated test file.
/// </summary>
public sealed class NilCoalesceDescriptorSymmetryTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

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

    // -----------------------------------------------------------------------
    // The array side — previously Unknown through '??', now resolves the element type.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayResult_ThroughNilCoalesce_IndexResolvesToElementType() {
        // Before this fix: 'first' typed Unknown (ArrayDescriptorOf had no BinaryExpr arm),
        // so this assignment failed E0001 ("Cannot assign value of type 'unknown'").
        DiagnosticBag bag = Check("""
            xs: int[]? := [1, 2, 3]
            first := (xs ?? [])[0]
            y: int := first
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // The map side — D-401's own fix, kept green and restated here so the symmetry with
    // the array case above is visible in one place rather than only in MapTypeDescriptorTests.
    // -----------------------------------------------------------------------

    [Fact]
    public void MapResult_ThroughNilCoalesce_IndexResolvesToValueType() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>?): void {
            n: int? := (m ?? map<string, int>{})["k"]
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Nested '??' — the descriptor arm recurses on both operands, so a chain of two or
    // more nil-coalesced operands resolves just as well as a single one.
    // -----------------------------------------------------------------------

    [Fact]
    public void NestedNilCoalesce_ArrayIndexResolvesElementType() {
        DiagnosticBag bag = Check("""
            a: int[]? := nil
            b: int[]? := nil
            c: int[] := [1, 2, 3]
            y: int := (a ?? b ?? c)[0]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void NestedNilCoalesce_MapIndexResolvesValueType() {
        DiagnosticBag bag = Check("""
            fn f(a: map<string, int>?, b: map<string, int>?): void {
            c: map<string, int> := map<string, int>{}
            y: int? := (a ?? b ?? c)["k"]
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // D-403: the function-descriptor side — ExpressionDescriptor's new NilCoalesce arm.
    // '(f ?? g)()' previously fell to Unknown (no BinaryExpr arm), which VisitCall's
    // ExpressionDescriptor consultation could not resolve, so the call's return type
    // stayed Unknown regardless of f/g's declared return type.
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionResult_ThroughNilCoalesce_CallResolvesToDeclaredReturnType() {
        var (unit, bag) = TypeCheckSource("""
            f: (fn(): int)? := () => 1
            g: fn(): int := () => 2
            y := (f ?? g)()
            """);

        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Int, call.ResolvedReturnType);
    }

    [Fact]
    public void NestedNilCoalesce_FunctionCallResolvesToDeclaredReturnType() {
        var (unit, bag) = TypeCheckSource("""
            f: (fn(): int)? := nil
            g: (fn(): int)? := nil
            h: fn(): int := () => 3
            y := (f ?? g ?? h)()
            """);

        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Int, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // D-403: the named-struct-type-identity side — GetStructTypeName's new NilCoalesce
    // arm. '(g1 ?? g2).toString()' previously fell to Unknown (no BinaryExpr arm), so
    // ResolveMemberAccessCall's NamedTypeRegistry lookup for the flattened receiver name
    // never fired and the call stayed permissively Unknown regardless of guid's real
    // 'toString' method surface.
    // -----------------------------------------------------------------------

    [Fact]
    public void GuidResult_ThroughNilCoalesce_ToStringResolvesViaNamedTypeDispatch() {
        var (unit, bag) = TypeCheckSource("""
            g1: guid? := guid.newV4()
            g2 := guid.newV4()
            s := (g1 ?? g2).toString()
            """);

        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        CallExpr call = Assert.Single(CollectCalls(unit), c => c.Callee is MemberAccessExpr { Member: "toString" });
        Assert.Equal(GrobType.String, call.ResolvedReturnType);
    }

    [Fact]
    public void NestedNilCoalesce_GuidToStringResolvesViaNamedTypeDispatch() {
        var (unit, bag) = TypeCheckSource("""
            g1: guid? := nil
            g2: guid? := nil
            g3 := guid.newV4()
            s := (g1 ?? g2 ?? g3).toString()
            """);

        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        CallExpr call = Assert.Single(CollectCalls(unit), c => c.Callee is MemberAccessExpr { Member: "toString" });
        Assert.Equal(GrobType.String, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // PR #189 review (CodeRabbit): the descriptor-retention arms above keep the LEFT
    // operand's side-channel identity, so '??' must first prove the two operands agree
    // on it. ResolveNilCoalesce's own element-kind check compares only the flat GrobType
    // tag, which is identical for every function ('Function'), every array ('Array') and
    // every named/anonymous struct ('Struct') regardless of signature, element type or
    // nominal identity — exactly the hole D-401 already closed for 'Map' via
    // MapValueAssignable. Same defect class, same remedy, same diagnostic (E0002).
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionResult_MismatchedNilCoalesceDescriptors_RaisesE0002() {
        // Without the check, '(f ?? g)()' is typed int even when g — returning string —
        // is the operand that actually runs.
        DiagnosticBag bag = Check("""
            f: (fn(): int)? := nil
            g: fn(): string := () => "s"
            y := (f ?? g)()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void NestedNilCoalesce_MismatchedFunctionDescriptors_RaisesE0002() {
        // The arm recurses, so the mismatch check must recurse with it: the offending
        // pair here is the inner '(f ?? g)', two levels down from the outer call.
        DiagnosticBag bag = Check("""
            f: (fn(): int)? := nil
            g: (fn(): string)? := nil
            h: fn(): int := () => 3
            y := (f ?? g ?? h)()
            """);

        // One diagnostic, not two: the inner '??' resolves to Error, and Error's universal
        // assignability keeps the outer '??' from cascading a second report.
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void FunctionResult_MatchedNilCoalesceDescriptors_StaysClean() {
        // Positive control for the check above — identical signatures must not trip it.
        DiagnosticBag bag = Check("""
            f: (fn(int): int)? := nil
            g: fn(int): int := (n: int) => n
            y := (f ?? g)(1)
            """);

        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void NamedStruct_MismatchedNilCoalesceOperands_RaisesE0002() {
        // 'date? ?? guid' both carry the flat Struct tag, so without the nominal check
        // GetStructTypeName keeps "date" and dispatches date's method table over a guid.
        DiagnosticBag bag = Check("""
            d: date? := nil
            g := guid.newV4()
            s := (d ?? g).toString()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void NamedStruct_MatchedNilCoalesceOperands_StaysClean() {
        // Positive control — the guid/guid pair the D-403 tests above rely on.
        DiagnosticBag bag = Check("""
            g1: guid? := nil
            g2 := guid.newV4()
            s := (g1 ?? g2).toString()
            """);

        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void AnonStruct_MismatchedNilCoalesceShapes_RaisesE0002() {
        // PR #189 follow-up review (CodeRabbit): the fifth instance of the same shape.
        // 'xs.first()' on an anonymous-struct array yields NullableAnonStruct, whose flat
        // tag matches every other anonymous shape — so before this arm, '(a ?? b).x'
        // type-checked clean against the LEFT operand's field table while 'b', which has
        // no 'x' at all, is the operand that runs when 'a' is nil. Confirmed by its twin:
        // '(a ?? b).y' reported "Type 'x:Int' has no member 'y'", naming the left shape.
        DiagnosticBag bag = Check("""
            xs := [#{ x: 1 }]
            a := xs.first()
            b := #{ y: 2 }
            c := (a ?? b).x
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void AnonStruct_MatchedNilCoalesceShapes_StaysClean() {
        // Positive control — identical synthesised shapes must still coalesce.
        DiagnosticBag bag = Check("""
            xs := [#{ x: 1 }]
            a := xs.first()
            b := #{ x: 2 }
            c := (a ?? b).x
            """);

        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void ArrayResult_MismatchedNilCoalesceElementTypes_RaisesE0002() {
        // The third instance of the same shape, unflagged by the review but structurally
        // identical: 'int[]? ?? string[]' passes the flat-Array tag check, then
        // ArrayDescriptorOf keeps the left (int) element descriptor.
        DiagnosticBag bag = Check("""
            a: int[]? := nil
            b: string[] := ["s"]
            y := (a ?? b)[0]
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }
}
