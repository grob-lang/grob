using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 9 Increment C0b-2b (D-378) — the map in-place-mutating
/// member surface: <c>set(key: K, value: V)</c>, <c>remove(key: K)</c>, <c>clear()</c>.
/// Completes the <c>map&lt;K, V&gt;</c> surface D-374/D-376/D-377 began, mirroring the
/// array precedent (D-373) exactly: the compile-time <c>readonly</c> rejection (E0204)
/// reused at a method-call site via <c>FindReadonlyRoot</c>, arity via
/// <c>MemberArgCountMatches</c> (E0003), and per-argument type checks (E0004).
/// </summary>
public sealed class TypeCheckerMapMutatingMemberTests {
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

    private static void AssertHasError(DiagnosticBag bag, string code, int line, int column) {
        Diagnostic diag = Assert.Single(bag.Errors, d => d.Code == code);
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

    // -----------------------------------------------------------------------
    // Basic resolution — all three resolve GrobType.Unknown (void), matching the array
    // mutating members' precedent.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.set(\"b\", 2)\n")]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.remove(\"a\")\n")]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.clear()\n")]
    public void MutatingCall_NoDiagnostics_ResolvesToUnknown(string source) {
        var (unit, bag) = TypeCheckSource(source);

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Unknown, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // Key/value type checking. Key reuses CheckMapKeyArgument (D-377); value is checked
    // by the new CheckMapValueArgument against the map's value-type descriptor.
    // -----------------------------------------------------------------------

    [Fact]
    public void Set_WrongTypedKeyArgument_ReportsE0004() {
        // Key argument 42 sits at line 2, column 7 (after `m.set(`).
        AssertSingleError(Check("m := map<string, int>{\"a\": 1}\nm.set(42, 1)\n"), "E0004", 2, 7);
    }

    [Fact]
    public void Set_WrongTypedValueArgument_ReportsE0004() {
        // Value argument "x" sits at line 2, column 12 (after `m.set("a", `).
        AssertSingleError(Check("m := map<string, int>{\"a\": 1}\nm.set(\"a\", \"x\")\n"), "E0004", 2, 12);
    }

    [Fact]
    public void Remove_WrongTypedKeyArgument_ReportsE0004() {
        // Key argument 42 sits at line 2, column 10 (after `m.remove(`).
        AssertSingleError(Check("m := map<string, int>{\"a\": 1}\nm.remove(42)\n"), "E0004", 2, 10);
    }

    [Theory]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.set(\"b\", 2)\n")]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.remove(\"a\")\n")]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.clear()\n")]
    public void CorrectlyTypedArguments_NoDiagnostics(string source) {
        DiagnosticBag bag = Check(source);
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    // -----------------------------------------------------------------------
    // Arity — E0003 for all three. The E0003 range is the whole call (node.Range),
    // which starts at the receiver (line 2, column 1 for every case below).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.set(\"a\")\n")]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.set(\"a\", 1, 2)\n")]
    public void Set_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003", 2, 1);
    }

    [Theory]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.remove()\n")]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.remove(\"a\", \"b\")\n")]
    public void Remove_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003", 2, 1);
    }

    [Fact]
    public void Clear_WithArgument_ReportsE0003() {
        AssertSingleError(Check("m := map<string, int>{\"a\": 1}\nm.clear(1)\n"), "E0003", 2, 1);
    }

    // -----------------------------------------------------------------------
    // readonly rejection — the load-bearing test (D-372/D-373 precedent). The E0204
    // range is the member access (memberAccess.Range), which starts at the receiver.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("readonly m := map<string, int>{\"a\": 1}\nm.set(\"b\", 2)\n")]
    [InlineData("readonly m := map<string, int>{\"a\": 1}\nm.remove(\"a\")\n")]
    [InlineData("readonly m := map<string, int>{\"a\": 1}\nm.clear()\n")]
    public void MutatingCall_OnReadonlyBinding_ReportsE0204(string source) {
        AssertSingleError(Check(source), "E0204", 2, 1);
    }

    [Fact]
    public void MutatingCall_OnReadonlyBinding_ChainedThroughStructField_ReportsE0204() {
        // Proves FindReadonlyRoot's walk through a MemberAccessExpr chain to the root
        // readonly binding also covers a map-valued struct field, not just a bare
        // identifier receiver. The chain roots at `b` (line 3, column 1).
        DiagnosticBag bag = Check(
            "type Box { items: map<string, int> }\n" +
            "readonly b := Box { items: map<string, int>{\"a\": 1} }\n" +
            "b.items.set(\"b\", 2)\n");
        AssertSingleError(bag, "E0204", 3, 1);
    }

    [Fact]
    public void MutatingCall_OnNonReadonlyBinding_NoE0204() {
        DiagnosticBag bag = Check("m := map<string, int>{\"a\": 1}\nm.set(\"b\", 2)\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    // -----------------------------------------------------------------------
    // Independent-diagnostic collection (invariant: all compile-time errors collected,
    // no cap). A readonly receiver AND a bad call are independent root causes — E0204
    // must not suppress the arity/argument-type diagnostic.
    // -----------------------------------------------------------------------

    [Fact]
    public void MutatingCall_OnReadonlyBinding_WithWrongArity_ReportsBothE0204AndE0003() {
        DiagnosticBag bag = Check("readonly m := map<string, int>{\"a\": 1}\nm.set(\"a\")\n");
        Assert.Equal(2, bag.Errors.Count());
        AssertHasError(bag, "E0204", 2, 1);
        AssertHasError(bag, "E0003", 2, 1);
    }

    [Fact]
    public void MutatingCall_OnReadonlyBinding_WithWrongTypedArgument_ReportsBothE0204AndE0004() {
        DiagnosticBag bag = Check("readonly m := map<string, int>{\"a\": 1}\nm.set(\"a\", \"x\")\n");
        Assert.Equal(2, bag.Errors.Count());
        AssertHasError(bag, "E0204", 2, 1);
        AssertHasError(bag, "E0004", 2, 12);
    }
}
