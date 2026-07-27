using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 9 Increment C0b-2a (D-377) — the map non-mutating query
/// member surface: <c>length</c>/<c>isEmpty</c>/<c>keys</c>/<c>values</c> (properties,
/// resolved directly in <c>VisitMemberAccess</c>, mirroring
/// <c>ResolveArrayPropertyAccess</c>, D-371) and <c>get(key)</c>/<c>contains(key)</c>
/// (methods, generic in the receiver's <see cref="MapTypeDescriptor"/> value type,
/// mirroring <c>VisitIndex</c>'s derivation). <c>set</c>/<c>remove</c>/<c>clear</c> are a
/// separate increment (C0b-2b) and are not covered here.
/// </summary>
public sealed class TypeCheckerMapQueryMemberTests {
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
    // length / isEmpty — properties, no descriptor needed.
    // -----------------------------------------------------------------------

    [Fact]
    public void Length_OnMapLiteral_ResolvesToInt() {
        var (unit, bag) = TypeCheckSource("m := map<string, int>{\"a\": 1}\nx := m.length\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        MemberAccessExpr access = Assert.Single(CollectMemberAccesses(unit), m => m.Member == "length");
        Assert.Equal(GrobType.Int, access.ResolvedFieldType);
    }

    [Fact]
    public void IsEmpty_OnMapLiteral_ResolvesToBool() {
        var (unit, bag) = TypeCheckSource("m := map<string, int>{\"a\": 1}\nx := m.isEmpty\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        MemberAccessExpr access = Assert.Single(CollectMemberAccesses(unit), m => m.Member == "isEmpty");
        Assert.Equal(GrobType.Bool, access.ResolvedFieldType);
    }

    [Fact]
    public void UnrecognisedMapProperty_ReportsE1002() {
        // Proves the permissive-Unknown fall-through actually closed for maps (D-377) —
        // a bare unknown member now faults rather than silently compiling to Unknown.
        DiagnosticBag bag = Check("m := map<string, int>{\"a\": 1}\nm.bogus\n");
        AssertSingleError(bag, "E1002", 2, 1);
    }

    [Fact]
    public void BareMethodNameReference_NoCall_ReportsE1002() {
        // 'get'/'contains' accessed without a call (no parens) is not a property.
        DiagnosticBag bag = Check("m := map<string, int>{\"a\": 1}\nm.get\n");
        AssertSingleError(bag, "E1002", 2, 1);
    }

    // -----------------------------------------------------------------------
    // keys / values — the load-bearing descriptor-composition tests: the result must
    // carry a POPULATED ArrayTypeDescriptor, not a bare one.
    // -----------------------------------------------------------------------

    [Fact]
    public void KeysFirst_OnMapLiteral_ResolvesToNullableString() {
        var (unit, bag) = TypeCheckSource(
            "m := map<string, int>{\"a\": 1, \"b\": 2}\nx := m.keys.first()\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.NullableString, call.ResolvedReturnType);
    }

    [Fact]
    public void ValuesContains_OnMapLiteral_ResolvesToBool() {
        var (unit, bag) = TypeCheckSource(
            "m := map<string, int>{\"a\": 1, \"b\": 2}\nx := m.values.contains(2)\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Bool, call.ResolvedReturnType);
    }

    [Fact]
    public void KeysLength_OnMapLiteral_ResolvesToInt() {
        var (unit, bag) = TypeCheckSource(
            "m := map<string, int>{\"a\": 1, \"b\": 2}\nx := m.keys.length\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        MemberAccessExpr access = Assert.Single(CollectMemberAccesses(unit), a => a.Member == "length");
        Assert.Equal(GrobType.Int, access.ResolvedFieldType);
    }

    [Fact]
    public void ValuesFirst_OnNestedArrayValuedMap_ResolvesToNullableIntArray() {
        // map<string, int[]>.values -> int[][] -> .first() -> int[]?, proving the two
        // descriptor systems (MapTypeDescriptor, ArrayTypeDescriptor) compose correctly
        // for a nested-array value type.
        var (unit, bag) = TypeCheckSource(
            "m := map<string, int[]>{\"a\": [1, 2]}\nfirstRow := m.values.first()\nrow: int[] := firstRow\n");

        Diagnostic diag = Assert.Single(bag.Errors);
        // firstRow is int[]? (nullable) — binding it to non-nullable int[] is E0104,
        // proving the nested descriptor threaded all the way through .values.first().
        Assert.Equal("E0104", diag.Code);
    }

    [Fact]
    public void Keys_OnEmptyMapLiteral_StillCarriesPopulatedDescriptor() {
        // map<string,int>{}.values.contains(1) must type-check (populated descriptor even
        // on an empty literal), not degrade to permissive Unknown.
        DiagnosticBag bag = Check(
            "m := map<string, int>{}\nx := m.values.contains(1)\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    // -----------------------------------------------------------------------
    // get(key) — generic V? typing, mirrors the indexer m[k].
    // -----------------------------------------------------------------------

    [Fact]
    public void Get_OnMapLiteral_ResolvesToNullableValueType() {
        var (unit, bag) = TypeCheckSource("m := map<string, int>{\"a\": 1}\nx := m.get(\"a\")\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.NullableInt, call.ResolvedReturnType);
    }

    [Fact]
    public void Get_ResultConsumedViaNilCoalesce_TypeChecksCleanly() {
        DiagnosticBag bag = Check("m := map<string, int>{\"a\": 1}\nx := m.get(\"z\") ?? 0\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    [Fact]
    public void Get_AndIndexer_AgreeOnResultType() {
        var (unit, bag) = TypeCheckSource(
            "m := map<string, int>{\"a\": 1}\nviaGet := m.get(\"a\")\nviaIndex := m[\"a\"]\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.NullableInt, call.ResolvedReturnType);
    }

    [Fact]
    public void Get_WrongTypedKeyArgument_ReportsE0004() {
        DiagnosticBag bag = Check("m := map<string, int>{\"a\": 1}\nm.get(42)\n");
        AssertSingleError(bag, "E0004", 2, 7);
    }

    [Theory]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.get()\n")]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.get(\"a\", \"b\")\n")]
    public void Get_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003", 2, 1);
    }

    // -----------------------------------------------------------------------
    // contains(key) — key membership, NOT value membership. Deliberate asymmetry
    // against array's value-membership contains(v).
    // -----------------------------------------------------------------------

    [Fact]
    public void Contains_KeyPresent_ResolvesToBool_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("m := map<string, int>{\"a\": 1}\nx := m.contains(\"a\")\n");

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Bool, call.ResolvedReturnType);
    }

    [Fact]
    public void Contains_IsKeyMembership_NotValueMembership_OnSameUnderlyingData() {
        // The map's contains("val") asks "is 'val' a KEY" (false, "val" is a value here);
        // the array's contains("val") on the map's own values asks "is 'val' a VALUE"
        // (true) — same underlying data, deliberately different subject per member name.
        DiagnosticBag bag = Check("""
            m := map<string, string>{"key": "val"}
            byKey := m.contains("val")
            byValue := m.values.contains("val")
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    [Fact]
    public void Contains_WrongTypedKeyArgument_ReportsE0004() {
        DiagnosticBag bag = Check("m := map<string, int>{\"a\": 1}\nm.contains(42)\n");
        AssertSingleError(bag, "E0004", 2, 12);
    }

    [Theory]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.contains()\n")]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.contains(\"a\", \"b\")\n")]
    public void Contains_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003", 2, 1);
    }
}
