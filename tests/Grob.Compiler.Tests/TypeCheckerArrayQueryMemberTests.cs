using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 9 Increment C0a-1 (D-371) — the array non-mutating
/// query member surface: <c>length</c>/<c>isEmpty</c> (properties, resolved directly in
/// <c>VisitMemberAccess</c> since arrays stay structural rather than joining either
/// registry, D-351/D-356/D-363), and <c>first()</c>/<c>last()</c>/<c>contains(v)</c>
/// (methods, generic in the receiver's <see cref="ArrayTypeDescriptor"/> element type,
/// mirroring <c>VisitIndex</c>'s derivation).
/// </summary>
public sealed class TypeCheckerArrayQueryMemberTests {
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

    private static void AssertSingleError(DiagnosticBag bag, string code, int line, int column) {
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(code, diag.Code);
        Assert.Equal(line, diag.Range.Start.Line);
        Assert.Equal(column, diag.Range.Start.Column);
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

    private static List<MemberAccessExpr> CollectMemberAccesses(CompilationUnit unit) {
        var collector = new MemberAccessCollector();
        collector.Visit(unit);
        return collector.Nodes;
    }

    private static List<CallExpr> CollectCalls(CompilationUnit unit) {
        var collector = new CallCollector();
        collector.Visit(unit);
        return collector.Nodes;
    }

    // -----------------------------------------------------------------------
    // length / isEmpty — properties.
    // -----------------------------------------------------------------------

    [Fact]
    public void Length_OnIntArray_ResolvesToInt() {
        var (unit, bag) = TypeCheckSource("xs: int[] := [1, 2, 3]\nx := xs.length\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        MemberAccessExpr access = Assert.Single(CollectMemberAccesses(unit), m => m.Member == "length");
        Assert.Equal(GrobType.Int, access.ResolvedFieldType);
    }

    [Fact]
    public void IsEmpty_OnIntArray_ResolvesToBool() {
        var (unit, bag) = TypeCheckSource("xs: int[] := [1, 2, 3]\nx := xs.isEmpty\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        MemberAccessExpr access = Assert.Single(CollectMemberAccesses(unit), m => m.Member == "isEmpty");
        Assert.Equal(GrobType.Bool, access.ResolvedFieldType);
    }

    [Fact]
    public void UnrecognisedArrayProperty_ReportsE1002() {
        // Proves the permissive-Unknown fall-through actually closed for arrays
        // (Sprint 9 Increment C0a-1, D-371) — a bare unknown member now faults rather
        // than silently compiling to Unknown.
        DiagnosticBag bag = Check("xs: int[] := [1, 2, 3]\nxs.bogus\n");
        AssertSingleError(bag, "E1002", 2, 1);
    }

    [Fact]
    public void BareMethodNameReference_NoCall_ReportsE1002() {
        // A method-family member accessed without a call ('.first', no parens) is not
        // a property — mirrors ResolveNamedTypePropertyAccess/
        // ResolvePrimitiveMemberPropertyAccess's identical rule for their own receivers.
        DiagnosticBag bag = Check("xs: int[] := [1, 2, 3]\nxs.first\n");
        AssertSingleError(bag, "E1002", 2, 1);
    }

    [Fact]
    public void UnrecognisedArrayMethodCall_ReportsE1002() {
        // D-380, mirroring D-371's property-path tightening onto the method-call path:
        // an unrecognised name reaches ValidateArrayMethodCall's default arm (previously
        // dead code, since IsArrayMethod gated it out before the switch ever saw it).
        DiagnosticBag bag = Check("xs: int[] := [1, 2, 3]\nxs.garbage()\n");
        AssertSingleError(bag, "E1002", 2, 1);
    }

    [Fact]
    public void UntypedLambdaParameterArrayMethodCall_StaysPermissive() {
        // The required survivor (D-380 scope boundary): an Unknown-typed receiver — here
        // an untyped lambda parameter — must keep compiling, never tightened by this change.
        DiagnosticBag bag = Check("xs: int[][] := [[1], [2]]\nxs.each(x => x.whatever())\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    [Fact]
    public void OptionalChainOnNullableArrayMethodCall_StaysPermissive() {
        // The other required survivor: '?.' on a nullable receiver stays permissive,
        // including for a name that would otherwise be unrecognised — NullableArray never
        // matches the tightened Array-receiver arm.
        DiagnosticBag bag = Check("xs: int[]? := nil\nxs?.garbage()\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    // -----------------------------------------------------------------------
    // first() / last() — generic T? typing, proven for two distinct element types.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("xs: int[] := [1, 2, 3]\nx := xs.first()\n", GrobType.NullableInt)]
    [InlineData("xs: string[] := [\"a\", \"b\"]\nx := xs.first()\n", GrobType.NullableString)]
    [InlineData("xs: int[] := [1, 2, 3]\nx := xs.last()\n", GrobType.NullableInt)]
    public void FirstLast_ResolvesToNullableElementType(string source, GrobType expected) {
        var (unit, bag) = TypeCheckSource(source);

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(expected, call.ResolvedReturnType);
    }

    [Theory]
    [InlineData("xs: int[] := [1, 2, 3]\nxs.first(1)\n")]
    [InlineData("xs: int[] := [1, 2, 3]\nxs.last(1)\n")]
    public void FirstLast_WithArgument_ReportsE0003(string source) {
        // first()/last() take no argument (D-371); the bound VM natives declare arity 0
        // and silently ignore any extra arg, so the type checker must reject it — via the
        // same MemberArgCountMatches gate contains() already uses.
        DiagnosticBag bag = Check(source);
        AssertSingleError(bag, "E0003", 2, 1);
    }

    [Fact]
    public void First_ResultConsumedViaNilCoalesce_TypeChecksCleanly() {
        // Proves the T? result is safely consumable via '??' — an empty array yields
        // nil at runtime, not a fault (D-371's empty-array behaviour).
        DiagnosticBag bag = Check("xs: int[] := []\nx := xs.first() ?? 0\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    [Fact]
    public void First_OnUntypedArrayParameter_StaysPermissiveUnknown() {
        // A receiver whose ArrayTypeDescriptor is unavailable (a bare 'array'-typed
        // parameter, no element type tracked) stays permissive, matching every other
        // missing-descriptor fallback in this file.
        var (unit, bag) = TypeCheckSource(
            "fn f(xs: array): int {\n  y := xs.first()\n  return 0\n}\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Unknown, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // contains(v: T) — argument-type checking against the element type.
    // -----------------------------------------------------------------------

    [Fact]
    public void Contains_MatchingElementType_NoDiagnostics_ResolvesToBool() {
        var (unit, bag) = TypeCheckSource("xs: int[] := [1, 2, 3]\nx := xs.contains(2)\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Bool, call.ResolvedReturnType);
    }

    [Fact]
    public void Contains_WrongTypedArgument_ReportsE0004() {
        DiagnosticBag bag = Check("xs: int[] := [1, 2, 3]\nxs.contains(\"x\")\n");
        AssertSingleError(bag, "E0004", 2, 13);
    }

    [Theory]
    [InlineData("xs: int[] := [1, 2, 3]\nxs.contains()\n")]
    [InlineData("xs: int[] := [1, 2, 3]\nxs.contains(1, 2)\n")]
    public void Contains_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003", 2, 1);
    }

    [Fact]
    public void Contains_NominalStructMismatch_ReportsE0004() {
        // date and guid are both flat GrobType.Struct; a flat TypesAreAssignable check
        // would accept date[].contains(guid). Nominal identity (D-303) must reject it,
        // mirroring the typed-function-argument nominal check (PR #133).
        DiagnosticBag bag = Check(
            "ds: date[] := [date.of(2026, 1, 1)]\ng := guid.newV4()\nx := ds.contains(g)\n");
        AssertSingleError(bag, "E0004", 3, 18);
    }

    [Fact]
    public void Contains_NestedArrayElementMismatch_ReportsE0004() {
        // int[][] and string[][] are both flat GrobType.Array; nested-descriptor identity
        // (D-351) must reject int[][].contains(string[]), mirroring the typed-function
        // array-argument check.
        DiagnosticBag bag = Check(
            "xs: int[][] := [[1, 2], [3, 4]]\nys: string[] := [\"a\", \"b\"]\nx := xs.contains(ys)\n");
        AssertSingleError(bag, "E0004", 3, 18);
    }

    [Fact]
    public void Contains_OnUntypedArrayParameter_StaysPermissive() {
        // No descriptor available — no argument-type check runs; always bool.
        var (unit, bag) = TypeCheckSource(
            "fn f(xs: array): bool {\n  return xs.contains(1)\n}\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Bool, call.ResolvedReturnType);
    }
}
