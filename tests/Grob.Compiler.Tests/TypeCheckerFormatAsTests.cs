using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 8 Increment E — the <c>formatAs</c> compiler-namespace
/// (D-342/D-282/D-320): the namespace-misuse diagnostics (bare <c>.formatAs</c>, unknown
/// <c>.formatAs.X</c>) and the plain function-form namespace-as-value fall-through.
/// </summary>
public sealed class TypeCheckerFormatAsTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private static IReadOnlyList<IdentifierExpr> CollectIdentifiers(CompilationUnit unit) {
        IdentifierCollector collector = new();
        collector.Visit(unit);
        return collector.Identifiers;
    }

    private static CallExpr SingleFormatAsCall(CompilationUnit unit) {
        CallCollector collector = new();
        collector.Visit(unit);
        return collector.Calls.Single(c => c.ResolvedFormatAsColumns is not null);
    }

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) {
            Identifiers.Add(node);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private sealed class CallCollector : AstWalker {
        public List<CallExpr> Calls { get; } = [];
        public override Unit VisitCall(CallExpr node) {
            Calls.Add(node);
            Visit(node.Callee);
            foreach (CallArgument arg in node.Arguments) Visit(arg.Value);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    // -----------------------------------------------------------------------
    // formatAs registered as an ordinary namespace name — bare identifier use
    // falls through to the existing generic D-342 E1004 arm unchanged.
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatAsAsValue_BareBinding_ReportsSingleE1004() {
        DiagnosticBag bag = Check("readonly x := formatAs");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
    }

    [Fact]
    public void FormatAsAsValue_ReceiverResolvesToNamespaceDecl() {
        var (unit, _) = TypeCheckSource("print(formatAs)");

        IdentifierExpr id = CollectIdentifiers(unit).First(i => i.Name == "formatAs");
        Assert.Same(UnresolvedDecl.Instance, id.Declaration);
    }

    // -----------------------------------------------------------------------
    // Bare '<expr>.formatAs' (no following method call) — the formatAs-specific
    // exact-wording error, folded into D-342's E1004.
    // -----------------------------------------------------------------------

    [Fact]
    public void BareFormatAsAccess_OnArray_ReportsSingleE1004WithExactMessage() {
        DiagnosticBag bag = Check("""
            arr := [1, 2, 3]
            readonly x := arr.formatAs
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
        Assert.Equal(
            "formatAs is a compiler-namespace, not a property. Use .formatAs.table(), .formatAs.list(), or .formatAs.csv().",
            diag.Message);
    }

    // -----------------------------------------------------------------------
    // '<expr>.formatAs.X()' where X isn't table/list/csv — the formatAs-specific
    // unknown-method error, folded into D-342's E1003, naming the three valid
    // methods.
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownFormatAsMethod_Called_ReportsSingleE1003NamingValidMethods() {
        DiagnosticBag bag = Check("""
            arr := [1, 2, 3]
            readonly x := arr.formatAs.frobnicate()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1003.Code, diag.Code);
        Assert.Contains("table", diag.Message);
        Assert.Contains("list", diag.Message);
        Assert.Contains("csv", diag.Message);
    }

    // -----------------------------------------------------------------------
    // Chained-form rewrite equivalence — the function form and the chained form
    // resolve identically for the same named-struct array.
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionForm_NamedStructArrayParameter_DerivesColumnsInDeclarationOrder() {
        var (unit, bag) = TypeCheckSource("""
            type Item {
                name: string
                price: float
            }
            fn report(items: Item[]): string {
                return formatAs.table(items)
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        CallExpr call = SingleFormatAsCall(unit);
        Assert.Equal(["name", "price"], call.ResolvedFormatAsColumns);
    }

    [Fact]
    public void ChainedForm_NamedStructArrayParameter_MatchesFunctionForm() {
        var (unit, bag) = TypeCheckSource("""
            type Item {
                name: string
                price: float
            }
            fn report(items: Item[]): string {
                return items.formatAs.table()
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        CallExpr call = SingleFormatAsCall(unit);
        Assert.Equal(["name", "price"], call.ResolvedFormatAsColumns);
    }

    // -----------------------------------------------------------------------
    // Anonymous-struct column derivation via a '.select(...)' projection —
    // the stdlib reference's own headline usage pattern.
    // -----------------------------------------------------------------------

    [Fact]
    public void ChainedForm_SelectProjectionToAnonStruct_DerivesColumnsFromLiteralFieldOrder() {
        var (unit, bag) = TypeCheckSource("""
            type Item {
                name: string
                price: float
            }
            fn report(items: Item[]): string {
                return items.select(i => #{ n: i.name, p: i.price }).formatAs.table()
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        CallExpr call = SingleFormatAsCall(unit);
        Assert.Equal(["n", "p"], call.ResolvedFormatAsColumns);
    }

    // -----------------------------------------------------------------------
    // '.filter(...)'/'.sort(...)' pass-through — neither changes element shape.
    // -----------------------------------------------------------------------

    [Fact]
    public void ChainedForm_FilterPassthrough_KeepsElementFieldOrder() {
        var (unit, bag) = TypeCheckSource("""
            type Item {
                name: string
                price: float
            }
            fn report(items: Item[]): string {
                return items.filter(i => i.price > 0.0).formatAs.table()
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        CallExpr call = SingleFormatAsCall(unit);
        Assert.Equal(["name", "price"], call.ResolvedFormatAsColumns);
    }

    [Fact]
    public void ChainedForm_SortPassthrough_KeepsElementFieldOrder() {
        var (unit, bag) = TypeCheckSource("""
            type Item {
                name: string
                price: float
            }
            fn report(items: Item[]): string {
                return items.sort(i => i.price).formatAs.table()
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        CallExpr call = SingleFormatAsCall(unit);
        Assert.Equal(["name", "price"], call.ResolvedFormatAsColumns);
    }

    // -----------------------------------------------------------------------
    // An indexed array element ('list' on 'arr[i]') shares its array's element shape.
    // Checker-only: array indexing has no compiler emission yet (a pre-existing gap
    // unrelated to formatAs), so this is verified at the type-check level, not run
    // through the VM.
    // -----------------------------------------------------------------------

    [Fact]
    public void ChainedForm_ListOnIndexedArrayElement_DerivesColumnsFromArrayElementShape() {
        var (unit, bag) = TypeCheckSource("""
            type Item {
                name: string
                price: float
            }
            fn report(items: Item[]): string {
                return items[0].formatAs.list()
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        CallExpr call = SingleFormatAsCall(unit);
        Assert.Equal(["name", "price"], call.ResolvedFormatAsColumns);
    }

    // -----------------------------------------------------------------------
    // Explicit 'columns:' selection (table only) — selects and reorders.
    // -----------------------------------------------------------------------

    [Fact]
    public void ChainedForm_ExplicitColumns_SelectsAndReorders() {
        var (unit, bag) = TypeCheckSource("""
            type Item {
                name: string
                price: float
                qty: int
            }
            fn report(items: Item[]): string {
                return items.formatAs.table(columns: ["price", "name"])
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        CallExpr call = SingleFormatAsCall(unit);
        Assert.Equal(["price", "name"], call.ResolvedFormatAsColumns);
    }

    [Fact]
    public void ExplicitColumns_UnknownFieldName_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""
            type Item {
                name: string
            }
            fn report(items: Item[]): string {
                return items.formatAs.table(columns: ["bogus"])
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
    }

    // -----------------------------------------------------------------------
    // Receiver-shape mismatch — argument-type-mismatch (E0004).
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionForm_TableOnNonArrayReceiver_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""readonly x := formatAs.table("not an array")""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
    }

    // -----------------------------------------------------------------------
    // 'list' — a single scalar struct value, not an array.
    // -----------------------------------------------------------------------

    [Fact]
    public void ChainedForm_ListOnNamedStruct_DerivesColumnsInDeclarationOrder() {
        var (unit, bag) = TypeCheckSource("""
            type Item {
                name: string
                price: float
            }
            fn report(item: Item): string {
                return item.formatAs.list()
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        CallExpr call = SingleFormatAsCall(unit);
        Assert.Equal(["name", "price"], call.ResolvedFormatAsColumns);
    }

    // -----------------------------------------------------------------------
    // 'csv' — same array-shape rules as 'table', minus 'columns:'.
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionForm_Csv_DerivesColumnsInDeclarationOrder() {
        var (unit, bag) = TypeCheckSource("""
            type Item {
                name: string
                price: float
            }
            fn report(items: Item[]): string {
                return formatAs.csv(items)
            }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        CallExpr call = SingleFormatAsCall(unit);
        Assert.Equal(["name", "price"], call.ResolvedFormatAsColumns);
    }

    // -----------------------------------------------------------------------
    // Statically indeterminate element shape — a plain 'array'-typed value with
    // no field registry to consult. Not a crash: a clear compile error (E0004).
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionForm_ArrayTypedParameterWithNoElementShape_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""
            fn report(items: array): string {
                return formatAs.table(items)
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
    }
}
