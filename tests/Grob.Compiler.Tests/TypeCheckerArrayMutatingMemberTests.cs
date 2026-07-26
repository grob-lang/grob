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

    /// <summary>
    /// Asserts the bag holds exactly one diagnostic and that it carries the full contract —
    /// exact code, 1-based line and 1-based column (equality, never an inequality, so an
    /// off-by-one in source-location tracking cannot hide).
    /// </summary>
    private static void AssertSingleError(DiagnosticBag bag, string code, int line, int column) {
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(code, diag.Code);
        Assert.Equal(line, diag.Range.Start.Line);
        Assert.Equal(column, diag.Range.Start.Column);
    }

    /// <summary>
    /// Asserts the bag holds exactly one diagnostic with <paramref name="code"/> and that
    /// its location matches — used where more than one independent diagnostic is expected
    /// and each must be pinned by code and position.
    /// </summary>
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
    // The E0004 range is the offending argument expression (argExpr.Range).
    // -----------------------------------------------------------------------

    [Fact]
    public void Append_WrongTypedArgument_ReportsE0004() {
        // Argument "x" sits at line 2, column 11 (after `xs.append(`).
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.append(\"x\")\n"), "E0004", 2, 11);
    }

    [Fact]
    public void Insert_WrongTypedValueArgument_ReportsE0004() {
        // Value argument "x" sits at line 2, column 14 (after `xs.insert(0, `).
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.insert(0, \"x\")\n"), "E0004", 2, 14);
    }

    [Fact]
    public void Insert_WrongTypedIndexArgument_ReportsE0004() {
        // Index argument "x" sits at line 2, column 11 (after `xs.insert(`).
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.insert(\"x\", 3)\n"), "E0004", 2, 11);
    }

    [Fact]
    public void Remove_WrongTypedIndexArgument_ReportsE0004() {
        // Index argument "x" sits at line 2, column 11 (after `xs.remove(`).
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.remove(\"x\")\n"), "E0004", 2, 11);
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
    // Arity — E0003 for all four. The E0003 range is the whole call (node.Range),
    // which starts at the receiver (line 2, column 1 for every case below).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("xs: int[] := [1, 2]\nxs.append()\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.append(1, 2)\n")]
    public void Append_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003", 2, 1);
    }

    [Theory]
    [InlineData("xs: int[] := [1, 2]\nxs.insert(0)\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.insert(0, 1, 2)\n")]
    public void Insert_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003", 2, 1);
    }

    [Theory]
    [InlineData("xs: int[] := [1, 2]\nxs.remove()\n")]
    [InlineData("xs: int[] := [1, 2]\nxs.remove(0, 1)\n")]
    public void Remove_WrongArity_ReportsE0003(string source) {
        AssertSingleError(Check(source), "E0003", 2, 1);
    }

    [Fact]
    public void Clear_WithArgument_ReportsE0003() {
        AssertSingleError(Check("xs: int[] := [1, 2]\nxs.clear(1)\n"), "E0003", 2, 1);
    }

    // -----------------------------------------------------------------------
    // readonly rejection — the load-bearing test (D-372/D-373). FindReadonlyRoot,
    // previously only reached from assignment/compound-assignment/increment targets,
    // is now reached from a method-call site for the first time. The E0204 range is
    // the member access (memberAccess.Range), which starts at the receiver.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("readonly xs := [1, 2]\nxs.append(3)\n")]
    [InlineData("readonly xs := [1, 2]\nxs.insert(0, 3)\n")]
    [InlineData("readonly xs := [1, 2]\nxs.remove(0)\n")]
    [InlineData("readonly xs := [1, 2]\nxs.clear()\n")]
    public void MutatingCall_OnReadonlyBinding_ReportsE0204(string source) {
        AssertSingleError(Check(source), "E0204", 2, 1);
    }

    [Fact]
    public void MutatingCall_OnReadonlyBinding_ChainedThroughStructField_ReportsE0204() {
        // Proves FindReadonlyRoot's walk through a MemberAccessExpr chain to the root
        // readonly binding, not just a bare identifier receiver. The chain roots at `b`
        // (line 3, column 1).
        DiagnosticBag bag = Check(
            "type Box { items: int[] }\nreadonly b := Box { items: [1, 2] }\nb.items.append(3)\n");
        AssertSingleError(bag, "E0204", 3, 1);
    }

    [Fact]
    public void MutatingCall_OnNonReadonlyBinding_NoE0204() {
        DiagnosticBag bag = Check("xs := [1, 2]\nxs.append(3)\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    // -----------------------------------------------------------------------
    // Independent-diagnostic collection (invariant: all compile-time errors collected,
    // no cap). A readonly receiver AND a bad call are independent root causes — E0204
    // must not suppress the arity/argument-type diagnostic. Mirrors the assignment-target
    // readonly path (VisitAssignment), which already emits E0204 then continues.
    // -----------------------------------------------------------------------

    [Fact]
    public void MutatingCall_OnReadonlyBinding_WithWrongArity_ReportsBothE0204AndE0003() {
        DiagnosticBag bag = Check("readonly xs := [1, 2]\nxs.append()\n");
        Assert.Equal(2, bag.Errors.Count());
        AssertHasError(bag, "E0204", 2, 1);
        AssertHasError(bag, "E0003", 2, 1);
    }

    [Fact]
    public void MutatingCall_OnReadonlyBinding_WithWrongTypedArgument_ReportsBothE0204AndE0004() {
        DiagnosticBag bag = Check("readonly xs := [1, 2]\nxs.append(\"x\")\n");
        Assert.Equal(2, bag.Errors.Count());
        AssertHasError(bag, "E0204", 2, 1);
        AssertHasError(bag, "E0004", 2, 11);
    }
}
