using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 9 Increment C0a-2 (D-373) — the array in-place-mutating
/// member surface: <c>append(value: T)</c>, <c>insert(index: int, value: T)</c>,
/// <c>remove(index: int)</c>, <c>clear()</c>. Completes the <c>T[]</c> surface D-371
/// began. Also proves the compile-time <c>readonly</c> rejection (E0204) reused at a
/// method-call site via <c>FindReadonlyRoot</c> — the load-bearing check this increment
/// wires for the first time from a call site rather than an assignment target.
/// </summary>
public sealed class TypeCheckerArrayMutatingMemberTests {
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

    private static void AssertSingleError(DiagnosticBag bag, string code) {
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(code, diag.Code);
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
    // Basic resolution — all four resolve GrobType.Unknown (void), matching each().
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("xs: int[] := [1, 2]\nxs.append(3)\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.insert(0, 3)\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.remove(0)\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.clear()\n")]
    public void MutatingCall_NoDiagnostics_ResolvesToUnknown(string source) {
        var (unit, bag) = TypeCheckSource(source);

        Assert.False(bag.HasErrors, FormatErrors(bag));
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Unknown, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // Element-type checking: append/insert's value argument against the element type.
    // -----------------------------------------------------------------------

    [Fact]
    public void Append_WrongTypedArgument_ReportsE0004() {
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.append(\"x\")\n"), "E0004");
    }

    [Fact]
    public void Insert_WrongTypedValueArgument_ReportsE0004() {
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.insert(0, \"x\")\n"), "E0004");
    }

    [Fact]
    public void Insert_WrongTypedIndexArgument_ReportsE0004() {
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.insert(\"x\", 3)\n"), "E0004");
    }

    [Fact]
    public void Remove_WrongTypedIndexArgument_ReportsE0004() {
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.remove(\"x\")\n"), "E0004");
    }

    [Theory]
    [InlineData("xs: int[] := [1, 2]\nxs.append(3)\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.insert(0, 3)\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.remove(0)\n")]
    public void CorrectlyTypedArguments_NoDiagnostics(string source) {
        DiagnosticBag bag = Check(source);
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    [Theory]
    [InlineData("fn f(xs: array): void {\n  xs.append(\"anything\")\n}\n")]
    [InlineData("fn f(xs: array): void {\n  xs.insert(0, \"anything\")\n}\n")]
    public void MissingDescriptor_ElementTypeCheck_StaysPermissive(string source) {
        DiagnosticBag bag = Check(source);
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    // -----------------------------------------------------------------------
    // Arity — E0003 for all four.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("xs: int[] := [1, 2]\nxs.append()\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.append(1, 2)\n")]
    public void Append_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003");
    }

    [Theory]
    [InlineData("xs: int[] := [1, 2]\nxs.insert(0)\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.insert(0, 1, 2)\n")]
    public void Insert_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003");
    }

    [Theory]
    [InlineData("xs: int[] := [1, 2]\nxs.remove()\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.remove(0, 1)\n")]
    public void Remove_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003");
    }

    [Fact]
    public void Clear_WithArgument_ReportsE0003() {
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.clear(1)\n"), "E0003");
    }

    // -----------------------------------------------------------------------
    // readonly rejection — the load-bearing test (D-372/D-373). FindReadonlyRoot,
    // previously only reached from assignment/compound-assignment/increment targets,
    // is now reached from a method-call site for the first time.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("readonly xs := [1, 2]\nxs.append(3)\n")]
    [InlineData("readonly xs := [1, 2]\nxs.insert(0, 3)\n")]
    [InlineData("readonly xs := [1, 2]\nxs.remove(0)\n")]
    [InlineData("readonly xs := [1, 2]\nxs.clear()\n")]
    public void MutatingCall_OnReadonlyBinding_ReportsE0204(string source) {
        AssertSingleError(Check(source), "E0204");
    }

    [Fact]
    public void MutatingCall_OnReadonlyBinding_ChainedThroughStructField_ReportsE0204() {
        // Proves FindReadonlyRoot's walk through a MemberAccessExpr chain to the root
        // readonly binding, not just a bare identifier receiver.
        DiagnosticBag bag = Check(
            "type Box { items: int[] }\nreadonly b := Box { items: [1, 2] }\nb.items.append(3)\n");
        AssertSingleError(bag, "E0204");
    }

    [Fact]
    public void MutatingCall_OnNonReadonlyBinding_NoE0204() {
        DiagnosticBag bag = Check("xs := [1, 2]\nxs.append(3)\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }
}
