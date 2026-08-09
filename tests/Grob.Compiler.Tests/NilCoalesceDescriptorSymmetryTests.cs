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
}
