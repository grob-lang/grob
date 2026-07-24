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

    // -----------------------------------------------------------------------
    // first() / last() — generic T? typing, proven for two distinct element types.
    // -----------------------------------------------------------------------

    [Fact]
    public void First_OnIntArray_ResolvesToNullableInt() {
        var (unit, bag) = TypeCheckSource("xs: int[] := [1, 2, 3]\nx := xs.first()\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.NullableInt, call.ResolvedReturnType);
    }

    [Fact]
    public void First_OnStringArray_ResolvesToNullableString() {
        var (unit, bag) = TypeCheckSource(
            "xs: string[] := [\"a\", \"b\"]\nx := xs.first()\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.NullableString, call.ResolvedReturnType);
    }

    [Fact]
    public void Last_OnIntArray_ResolvesToNullableInt() {
        var (unit, bag) = TypeCheckSource("xs: int[] := [1, 2, 3]\nx := xs.last()\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.NullableInt, call.ResolvedReturnType);
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

    [Fact]
    public void Contains_TooFewArguments_ReportsE0003() {
        DiagnosticBag bag = Check("xs: int[] := [1, 2, 3]\nxs.contains()\n");
        AssertSingleError(bag, "E0003", 2, 1);
    }

    [Fact]
    public void Contains_TooManyArguments_ReportsE0003() {
        DiagnosticBag bag = Check("xs: int[] := [1, 2, 3]\nxs.contains(1, 2)\n");
        AssertSingleError(bag, "E0003", 2, 1);
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
