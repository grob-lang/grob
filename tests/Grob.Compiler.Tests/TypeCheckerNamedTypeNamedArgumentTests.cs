using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Change group 4 of the native named arguments increment: named-type methods bind named
/// arguments on the shared binder, with default fill, which this path never had (D-446
/// Decisions 1 and 2, D-447 Decision 1), and overloads are chosen on the positional
/// arguments alone. The tests run against <c>box</c>, a type the fixtures register through
/// the public surface, because no first-party named-type method has two defaults or a
/// float parameter.
/// </summary>
public sealed class TypeCheckerNamedTypeNamedArgumentTests {
    // fit(width, left = "<", right = ">") returns string; scale(factor: float); pick has an
    // overload that takes a function second.
    private static (CompilationUnit Unit, DiagnosticBag Bag) Check(string source) {
        var bag = new DiagnosticBag();
        CompilationUnit unit = Parser.Parse(Lexer.Scan(source, bag), bag);
        new TypeChecker(bag, MemberNamedArgumentFixtures.NamedType).Check(unit);
        return (unit, bag);
    }

    private static (CallExpr Call, DiagnosticBag Bag) CheckCall(string call) {
        (CompilationUnit unit, DiagnosticBag bag) = Check($"b := acmebox.make()\nv := {call}");
        return (FindLastCall(unit), bag);
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

    private static BoundSlot[] Slots(CallExpr call) {
        Assert.NotNull(call.ResolvedBinding);
        return [.. call.ResolvedBinding.Slots];
    }

    private static void AssertSingleError(DiagnosticBag bag, string code, string message, int column) {
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(code, diag.Code);
        Assert.Equal(message, diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(column, diag.Range.Start.Column);
    }

    private static readonly GrobValue Left = GrobValue.FromString("<");
    private static readonly GrobValue Right = GrobValue.FromString(">");

    // -----------------------------------------------------------------------
    // Default fill (new on this path), skip and fill, either order.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamedTypeCall_OmittedTrailingDefaults_FillsThemWithTheirConstants() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("b.fit(5)");

        Assert.False(bag.HasErrors);
        Assert.Equal(
            [new BoundSlot(0, null), new BoundSlot(BoundSlot.DefaultArgument, Left), new BoundSlot(BoundSlot.DefaultArgument, Right)],
            Slots(call));
    }

    [Fact]
    public void NamedTypeCall_NamedSkipsDefault_FillsTheSkippedSlotWithItsConstant() {
        // fit(5, right: "]") reaches the native as (5, "<", "]").
        (CallExpr call, DiagnosticBag bag) = CheckCall("""b.fit(5, right: "]")""");

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(0, null), new BoundSlot(BoundSlot.DefaultArgument, Left), new BoundSlot(1, null)], Slots(call));
    }

    [Theory]
    [InlineData("""b.fit(5, left: "[", right: "]")""", new[] { 0, 1, 2 })]
    [InlineData("""b.fit(5, right: "]", left: "[")""", new[] { 0, 2, 1 })]
    public void NamedTypeCall_NamedInEitherOrder_BindsEachToItsParameter(string call, int[] expected) {
        (CallExpr bound, DiagnosticBag bag) = CheckCall(call);

        Assert.False(bag.HasErrors);
        Assert.Equal(expected, Slots(bound).Select(s => s.ArgumentIndex));
    }

    // -----------------------------------------------------------------------
    // The four binding diagnostics, member wording (gate report §1.4).
    // -----------------------------------------------------------------------

    [Fact]
    public void NamedTypeCall_PositionalAfterNamed_ReportsE0008() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("""b.fit(5, left: "[", "]")""");

        AssertSingleError(bag, "E0008",
            "In the call to 'fit', a positional argument follows a named argument. Move all named arguments after the positional ones.",
            26);
        Assert.Null(call.ResolvedBinding);
    }

    [Fact]
    public void NamedTypeCall_NamedRequiredParameter_ReportsE0009() {
        (_, DiagnosticBag bag) = CheckCall("b.fit(width: 5)");

        AssertSingleError(bag, "E0009",
            "Parameter 'width' of 'fit' is required and has no default, so it cannot be passed by name. Pass it positionally.",
            12);
    }

    [Fact]
    public void NamedTypeCall_NamedTwice_ReportsE0010() {
        (_, DiagnosticBag bag) = CheckCall("""b.fit(5, left: "a", left: "b")""");

        AssertSingleError(bag, "E0010", "Parameter 'left' of 'fit' is already supplied.", 26);
    }

    [Fact]
    public void NamedTypeCall_UnknownName_ReportsE0011() {
        (_, DiagnosticBag bag) = CheckCall("""b.fit(5, zzz: "a")""");

        AssertSingleError(bag, "E0011", "'fit' has no parameter named 'zzz'.", 15);
    }

    [Fact]
    public void NamedTypeCall_BindingError_SuppressesArityAndTypeChecks() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("b.fit(zzz: 1)");

        AssertSingleError(bag, "E0011", "'fit' has no parameter named 'zzz'.", 12);
        Assert.Null(call.ResolvedBinding);
    }

    // -----------------------------------------------------------------------
    // Arity (E0003) and type (E0004) on the bound set; widening.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamedTypeCall_NamedDefaultWithRequiredMissing_ReportsE0003AtTheCall() {
        (_, DiagnosticBag bag) = CheckCall("""b.fit(left: "<")""");

        AssertSingleError(bag, "E0003", "'fit' expects an argument for required parameter 'width', but none was supplied.", 6);
    }

    [Fact]
    public void NamedTypeCall_TooManyPositionals_ReportsE0003() {
        (_, DiagnosticBag bag) = CheckCall("""b.fit(5, "a", "b", "c")""");

        AssertSingleError(bag, "E0003", "'fit' expects between 1 and 3 arguments, but 4 were supplied.", 6);
    }

    [Fact]
    public void NamedTypeCall_NamedArgumentWrongType_ReportsE0004AtTheArgument() {
        (_, DiagnosticBag bag) = CheckCall("b.fit(5, left: 9)");

        AssertSingleError(bag, "E0004",
            "Argument to 'fit' has type 'int', which is not assignable to parameter of type 'string'.",
            21);
    }

    [Fact]
    public void NamedTypeCall_IntArgumentToFloatParameter_MarksTheSlotForWidening() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("b.scale(2)");

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(0, null, WidenToFloat: true)], Slots(call));
    }

    [Fact]
    public void NamedTypeCall_ResolvesTheDeclaredReturnType() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("""b.fit(5, right: "]")""");

        Assert.False(bag.HasErrors);
        Assert.Equal(GrobType.String, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // Overloads: positional arguments choose; a name binds against the chosen one.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamedTypeCall_PositionalStringSecond_ChoosesTheFirstOverload() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("""b.pick("x", "y")""");

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(0, null), new BoundSlot(1, null)], Slots(call));
    }

    [Fact]
    public void NamedTypeCall_PositionalLambdaSecond_ChoosesTheFunctionOverload() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("""b.pick("x", s => s)""");

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(0, null), new BoundSlot(1, null)], Slots(call));
    }

    [Fact]
    public void NamedTypeCall_NameOnlyTheOtherOverloadDeclares_ReportsE0011AgainstTheChosenOne() {
        // Only one argument is positional, so no overload fits and the primary signature
        // takes the call: 'picker' is a parameter of the other overload alone.
        (_, DiagnosticBag bag) = CheckCall("b.pick(\"x\", picker: s => s)");

        AssertSingleError(bag, "E0011", "'pick' has no parameter named 'picker'.", 18);
    }

    // -----------------------------------------------------------------------
    // A nullable receiver binds the same way.
    // -----------------------------------------------------------------------

    [Fact]
    public void NullableNamedTypeCall_NamedSkip_Binds() {
        (CompilationUnit unit, DiagnosticBag bag) = Check("""
            fn show(nb: box?): string? {
                return nb?.fit(5, right: "]")
            }
            """);

        Assert.False(bag.HasErrors, string.Join("; ", bag.Errors.Select(d => d.Message)));
        Assert.Equal([new BoundSlot(0, null), new BoundSlot(BoundSlot.DefaultArgument, Left), new BoundSlot(1, null)], Slots(FindLastCall(unit)));
    }
}
