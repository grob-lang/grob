using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Change group 5 of the native named arguments increment: a callee that records no
/// parameter names refuses a named argument with E0011 (D-447 Decision 2, extended by
/// Chris's rulings S2 and S3), reported once per named argument at the argument, with the
/// arity and type checks suppressed, because binding would silently pass the values in
/// source order (R-20). Array methods, map methods, <c>print</c>, <c>exit</c> and a member
/// call on an <c>Unknown</c> receiver refuse; a call through a function value refused
/// already (D-448). <c>sort</c> alone binds <c>descending</c> (S2).
/// </summary>
public sealed class TypeCheckerNoSignatureNamedArgumentTests {
    private const string Prefix = "xs := [1, 2, 3]\nm := map<string, int>{\"a\": 1}\n";

    private static (CompilationUnit Unit, DiagnosticBag Bag) Check(string source) {
        var bag = new DiagnosticBag();
        CompilationUnit unit = Parser.Parse(Lexer.Scan(source, bag), bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag CheckCall(string call) => Check(Prefix + call).Bag;

    private static string Refusal(string callee) => $"'{callee}' does not accept named arguments; pass them positionally.";

    private const string FunctionValueRefusal = "A function value does not accept named arguments; pass them positionally.";

    private static void AssertRefusals(DiagnosticBag bag, string message, int line, params int[] columns) {
        Diagnostic[] errors = [.. bag.Errors];
        Assert.Equal(columns.Length, errors.Length);
        for (int i = 0; i < columns.Length; i++) {
            Assert.Equal("E0011", errors[i].Code);
            Assert.Equal(message, errors[i].Message);
            Assert.Equal(line, errors[i].Range.Start.Line);
            Assert.Equal(columns[i], errors[i].Range.Start.Column);
        }
    }

    private static void AssertSingleError(DiagnosticBag bag, string code, string message, int line, int column) {
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(code, diag.Code);
        Assert.Equal(message, diag.Message);
        Assert.Equal(line, diag.Range.Start.Line);
        Assert.Equal(column, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Array methods (kind 5, sort aside).
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayContains_NamedArgument_IsRefused() =>
        AssertRefusals(CheckCall("xs.contains(v: 2)"), Refusal("contains"), 3, 13);

    [Fact]
    public void ArrayInsert_ReorderedNamedArguments_AreRefusedEach() =>
        // D-447's own example: insert(value: 9, index: 0) put the value 0 at index 9.
        AssertRefusals(CheckCall("xs.insert(value: 9, index: 0)"), Refusal("insert"), 3, 11, 21);

    [Fact]
    public void ArrayAppend_NamedArgument_IsRefused() =>
        AssertRefusals(CheckCall("xs.append(value: 1)"), Refusal("append"), 3, 11);

    [Fact]
    public void ArrayRemove_NamedArgument_IsRefused() =>
        AssertRefusals(CheckCall("xs.remove(index: 0)"), Refusal("remove"), 3, 11);

    [Fact]
    public void ArrayFilter_NamedArgument_IsRefused() =>
        AssertRefusals(CheckCall("xs.filter(p: x => true)"), Refusal("filter"), 3, 11);

    [Fact]
    public void ArrayFilter_NamedArgumentWithANonBoolPredicate_IsRefusedWithoutAPredicateError() =>
        // The refused name bound to no parameter, so its lambda is not judged as the
        // predicate; the positional form still is (E0004).
        AssertRefusals(CheckCall("xs.filter(p: x => 1)"), Refusal("filter"), 3, 11);

    [Fact]
    public void ArrayFilter_PositionalNonBoolPredicate_StillReportsE0004() {
        Diagnostic diag = Assert.Single(CheckCall("xs.filter(x => 1)").Errors);
        Assert.Equal("E0004", diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(11, diag.Range.Start.Column);
    }

    [Fact]
    public void ArraySelect_NamedArgument_IsRefused() =>
        AssertRefusals(CheckCall("xs.select(p: x => x)"), Refusal("select"), 3, 11);

    [Fact]
    public void ArrayEach_NamedArgument_IsRefused() =>
        AssertRefusals(CheckCall("xs.each(p: x => print(x))"), Refusal("each"), 3, 9);

    [Fact]
    public void ArrayFirst_NamedArgument_IsRefusedWithoutAnArityError() =>
        AssertRefusals(CheckCall("xs.first(a: 1)"), Refusal("first"), 3, 10);

    [Fact]
    public void ArrayClear_NamedArgument_IsRefusedWithoutAnArityError() =>
        AssertRefusals(CheckCall("xs.clear(a: 1)"), Refusal("clear"), 3, 10);

    [Fact]
    public void NullableArrayContains_NamedArgument_IsRefused() {
        (_, DiagnosticBag bag) = Check("""
            fn has(xs: int[]?): bool? {
                return xs?.contains(v: 2)
            }
            """);

        AssertRefusals(bag, Refusal("contains"), 2, 25);
    }

    // -----------------------------------------------------------------------
    // Map methods (kind 6).
    // -----------------------------------------------------------------------

    [Fact]
    public void MapGet_NamedArgument_IsRefused() =>
        AssertRefusals(CheckCall("""m.get(key: "a")"""), Refusal("get"), 3, 7);

    [Fact]
    public void MapSet_ReorderedNamedArguments_AreRefusedEach() =>
        AssertRefusals(CheckCall("""m.set(value: 2, key: "b")"""), Refusal("set"), 3, 7, 17);

    [Fact]
    public void MapContains_NamedArgument_IsRefused() =>
        AssertRefusals(CheckCall("""m.contains(key: "a")"""), Refusal("contains"), 3, 12);

    [Fact]
    public void MapRemove_NamedArgument_IsRefused() =>
        AssertRefusals(CheckCall("""m.remove(key: "a")"""), Refusal("remove"), 3, 10);

    [Fact]
    public void MapClear_NamedArgument_IsRefusedWithoutAnArityError() =>
        AssertRefusals(CheckCall("m.clear(a: 1)"), Refusal("clear"), 3, 9);

    [Fact]
    public void ArrayInsert_PositionalCall_StillChecksArityAndTypes() {
        // The refusal replaces nothing for a call with no named argument.
        DiagnosticBag bag = CheckCall("xs.insert(0)");

        AssertSingleError(bag, "E0003", "'insert' expects 2 arguments, but 1 was supplied.", 3, 1);
    }

    // -----------------------------------------------------------------------
    // print and exit (kind 7).
    // -----------------------------------------------------------------------

    [Fact]
    public void Print_NamedArgument_IsRefused() =>
        AssertRefusals(Check("print(value: \"x\")").Bag, Refusal("print"), 1, 7);

    [Fact]
    public void Print_TwoNamedArguments_AreRefusedEachWithoutAnArityError() =>
        AssertRefusals(Check("print(value: 1, value: 2)").Bag, Refusal("print"), 1, 7, 17);

    [Fact]
    public void Exit_NamedArgument_IsRefused() =>
        AssertRefusals(Check("exit(code: 3)").Bag, Refusal("exit"), 1, 6);

    // -----------------------------------------------------------------------
    // A member call on a receiver with nothing to bind against (S3).
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownReceiverMemberCall_NamedArguments_AreRefusedEach() {
        // x has no shape, so x.insert checks against nothing; reordered, it inserted the
        // value 0 at index 9.
        (_, DiagnosticBag bag) = Check("lam := x => x.insert(value: 9, index: 0)");

        AssertRefusals(bag, Refusal("insert"), 1, 22, 32);
    }

    // -----------------------------------------------------------------------
    // Function values (kind 9): refused by D-448's check; these are the shapes the gate
    // report measured besides the three TypeCheckerFunctionTypeTests already pin.
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionReference_NamedArguments_AreRefusedEach() {
        // A reference to a declared function is typed Unknown, so its names bind to nothing.
        (_, DiagnosticBag bag) = Check("""
            fn sub(a: int, b: int): int {
                return a - b
            }
            fref := sub
            fref(b: 10, a: 3)
            """);

        AssertRefusals(bag, FunctionValueRefusal, 5, 6, 13);
    }

    [Fact]
    public void CallResult_NamedArgument_IsRefused() {
        (_, DiagnosticBag bag) = Check("""
            fn make(): fn(int): int {
                return x => x
            }
            make()(b: 1)
            """);

        AssertRefusals(bag, FunctionValueRefusal, 4, 8);
    }

    // -----------------------------------------------------------------------
    // sort binds descending (S2): fn is required, descending defaults to false.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArraySort_NamedDescending_BindsAndEmitsInSourceOrder() {
        (CompilationUnit unit, DiagnosticBag bag) = Check(Prefix + "xs.sort(x => x, descending: true)");

        Assert.False(bag.HasErrors, string.Join("; ", bag.Errors.Select(d => d.Message)));
        // descending can only follow a positional fn, so a clean call is already in
        // parameter order: sort binds for its diagnostics and gets no record.
        Assert.Null(FindLastCall(unit).ResolvedBinding);
    }

    private static CallExpr FindLastCall(CompilationUnit unit) {
        var finder = new LastCallFinder();
        finder.Visit(unit);
        return finder.Found ?? throw new InvalidOperationException("No CallExpr in the unit.");
    }

    private sealed class LastCallFinder : AstWalker {
        public CallExpr? Found { get; private set; }
        public override Unit VisitCall(CallExpr node) { Found = node; return base.VisitCall(node); }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    [Fact]
    public void ArraySort_PositionalDescending_StillBinds() =>
        Assert.False(CheckCall("xs.sort(x => x, true)").HasErrors);

    [Fact]
    public void ArraySort_KeyOnly_StillBinds() =>
        Assert.False(CheckCall("xs.sort(x => x)").HasErrors);

    [Fact]
    public void ArraySort_UnknownName_ReportsE0011WithTheBindingWording() =>
        AssertSingleError(CheckCall("xs.sort(x => x, banana: true)"),
            "E0011", "'sort' has no parameter named 'banana'.", 3, 17);

    [Fact]
    public void ArraySort_NamedTwice_ReportsE0010() =>
        AssertSingleError(CheckCall("xs.sort(x => x, descending: true, descending: false)"),
            "E0010", "Parameter 'descending' of 'sort' is already supplied.", 3, 35);

    [Fact]
    public void ArraySort_PositionalAfterNamed_ReportsE0008() =>
        AssertSingleError(CheckCall("xs.sort(descending: true, x => x)"),
            "E0008",
            "In the call to 'sort', a positional argument follows a named argument. Move all named arguments after the positional ones.",
            3, 27);

    [Fact]
    public void ArraySort_DescendingWithoutKey_ReportsE0003AtTheCall() =>
        AssertSingleError(CheckCall("xs.sort(descending: true)"),
            "E0003", "'sort' expects an argument for required parameter 'fn', but none was supplied.", 3, 1);

    [Fact]
    public void ArraySort_NoArguments_ReportsE0003() =>
        AssertSingleError(CheckCall("xs.sort()"),
            "E0003", "'sort' expects an argument for required parameter 'fn', but none was supplied.", 3, 1);

    [Fact]
    public void ArraySort_TooManyArguments_ReportsE0003() =>
        AssertSingleError(CheckCall("xs.sort(x => x, true, false)"),
            "E0003", "'sort' expects between 1 and 2 arguments, but 3 were supplied.", 3, 1);

    [Fact]
    public void ArraySort_NamedDescendingNotBool_ReportsE0004AtTheValue() =>
        AssertSingleError(CheckCall("xs.sort(x => x, descending: 5)"),
            "E0004", "'sort' second argument ('descending') must be 'bool'; found 'int'.", 3, 29);
}
