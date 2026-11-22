using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Runtime;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Change group 2 of the native named arguments increment: namespace natives and
/// <c>input</c> bind named arguments on the shared binder (D-446 Decisions 1 and 2,
/// D-447 Decision 1). The convention and its four diagnostics are the script-function
/// ones; only the callee's name differs, as native arity messages already name it
/// (gate report §1.4).
/// </summary>
public sealed class TypeCheckerNativeNamedArgumentTests {
    private static (CallExpr Call, DiagnosticBag Bag) CheckFirstCall(string source) {
        var bag = new DiagnosticBag();
        CompilationUnit unit = Parser.Parse(Lexer.Scan(source, bag), bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        var finder = new FirstCallFinder();
        finder.Visit(unit);
        return (finder.Found ?? throw new InvalidOperationException("No CallExpr in the unit."), bag);
    }

    private sealed class FirstCallFinder : AstWalker {
        public CallExpr? Found { get; private set; }
        public override Unit VisitCall(CallExpr node) { Found ??= node; return base.VisitCall(node); }
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
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(column, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Binding: a named argument binds; skip and fill; either order.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceCall_NamedDefaultedParameter_Binds() {
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall("""d := date.parse("05/04/2026", pattern: "dd/MM/yyyy")""");

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(0, null), new BoundSlot(1, null)], Slots(call));
    }

    [Fact]
    public void NamespaceCall_NamedSkipsDefault_FillsTheSkippedSlotWithItsConstant() {
        // regex.compile(pattern, flags = "", timeout = 5) is the one registered namespace
        // native with two defaults: regex.compile(p, timeout: 2) reaches it as (p, "", 2).
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall("""r := regex.compile("a", timeout: 2)""");

        Assert.False(bag.HasErrors);
        Assert.Equal(
            [new BoundSlot(0, null), new BoundSlot(BoundSlot.DefaultArgument, GrobValue.FromString("")), new BoundSlot(1, null)],
            Slots(call));
    }

    [Theory]
    [InlineData("""r := regex.compile("a", flags: "i", timeout: 2)""", new[] { 0, 1, 2 })]
    [InlineData("""r := regex.compile("a", timeout: 2, flags: "i")""", new[] { 0, 2, 1 })]
    public void NamespaceCall_NamedInEitherOrder_BindsEachToItsParameter(string source, int[] expected) {
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall(source);

        Assert.False(bag.HasErrors);
        Assert.Equal(expected, Slots(call).Select(s => s.ArgumentIndex));
    }

    [Fact]
    public void NamespaceCall_OmittedTrailingDefault_FillsItWithItsConstant() {
        (CallExpr call, _) = CheckFirstCall("""d := date.parse("2026-04-05")""");

        Assert.Equal([new BoundSlot(0, null), new BoundSlot(BoundSlot.DefaultArgument, GrobValue.FromString(""))], Slots(call));
    }

    [Fact]
    public void NamespaceCall_CompletePositional_BindsEachSlotToItsArgument() {
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall("x := math.sqrt(2.0)");

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(0, null)], Slots(call));
    }

    [Fact]
    public void VariadicNamespaceCall_SetsNoRecord() {
        // A variadic native keeps positional emission: its tail cannot be named and has
        // no default, so there is nothing to reorder or fill.
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall("""p := path.join("a", "b")""");

        Assert.False(bag.HasErrors);
        Assert.Null(call.ResolvedBinding);
    }

    // -----------------------------------------------------------------------
    // The four binding diagnostics, native wording (gate report §1.4).
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceCall_PositionalAfterNamed_ReportsE0008() {
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall("""r := regex.compile("a", flags: "i", 2)""");

        AssertSingleError(bag, "E0008",
            "In the call to 'regex.compile', a positional argument follows a named argument. Move all named arguments after the positional ones.",
            37);
        Assert.Null(call.ResolvedBinding);
    }

    [Fact]
    public void NamespaceCall_NamedRequiredParameter_ReportsE0009() {
        (_, DiagnosticBag bag) = CheckFirstCall("x := math.pow(2.0, exp: 3.0)");

        AssertSingleError(bag, "E0009",
            "Parameter 'exp' of 'math.pow' is required and has no default, so it cannot be passed by name. Pass it positionally.",
            20);
    }

    [Fact]
    public void NamespaceCall_NamedTwice_ReportsE0010() {
        (_, DiagnosticBag bag) = CheckFirstCall("""d := date.parse("x", pattern: "a", pattern: "b")""");

        AssertSingleError(bag, "E0010", "Parameter 'pattern' of 'date.parse' is already supplied.", 36);
    }

    [Fact]
    public void NamespaceCall_UnknownName_ReportsE0011() {
        (_, DiagnosticBag bag) = CheckFirstCall("""d := date.parse("x", zzz: "a")""");

        AssertSingleError(bag, "E0011", "'date.parse' has no parameter named 'zzz'.", 22);
    }

    [Fact]
    public void VariadicNamespaceCall_NamedTail_ReportsE0009() {
        (_, DiagnosticBag bag) = CheckFirstCall("""p := path.join(parts: "a")""");

        AssertSingleError(bag, "E0009",
            "Parameter 'parts' of 'path.join' is required and has no default, so it cannot be passed by name. Pass it positionally.",
            16);
    }

    [Fact]
    public void VariadicNamespaceCall_UnknownName_ReportsE0011() {
        (_, DiagnosticBag bag) = CheckFirstCall("""p := path.join("a", zzz: "b")""");

        AssertSingleError(bag, "E0011", "'path.join' has no parameter named 'zzz'.", 21);
    }

    [Fact]
    public void NamespaceCall_BindingError_SuppressesArityAndTypeChecks() {
        // zzz: 1 is unknown, so value is also unbound (E0003) and 1 is not a string
        // (E0004); one diagnostic per root cause.
        (_, DiagnosticBag bag) = CheckFirstCall("d := date.parse(zzz: 1)");

        AssertSingleError(bag, "E0011", "'date.parse' has no parameter named 'zzz'.", 17);
    }

    // -----------------------------------------------------------------------
    // Arity (E0003) and type (E0004) on the bound set.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceCall_NamedDefaultWithRequiredMissing_ReportsE0003AtTheCall() {
        (_, DiagnosticBag bag) = CheckFirstCall("""d := date.parse(pattern: "dd")""");

        AssertSingleError(bag, "E0003", "'date.parse' expects between 1 and 2 arguments, but 1 was supplied.", 6);
    }

    [Fact]
    public void NamespaceCall_NamedArgumentWrongType_ReportsE0004AtItsWrittenPosition() {
        // The argument is written second and binds to the third parameter; the message
        // numbers it as written, where its range points (gate report §1.4).
        (_, DiagnosticBag bag) = CheckFirstCall("""r := regex.compile("a", timeout: "x")""");

        AssertSingleError(bag, "E0004",
            "Argument 2 to 'regex.compile' has type 'string', which is not assignable to parameter of type 'int'.",
            34);
    }

    // -----------------------------------------------------------------------
    // input: one defaulted parameter (D-447 Decision 1).
    // -----------------------------------------------------------------------

    [Fact]
    public void InputCall_NamedPrompt_Binds() {
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall("""s := input(prompt: "> ")""");

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(0, null)], Slots(call));
    }

    [Fact]
    public void InputCall_PromptOmitted_FillsItWithItsConstant() {
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall("s := input()");

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(BoundSlot.DefaultArgument, GrobValue.FromString(""))], Slots(call));
    }

    [Fact]
    public void InputCall_PositionalAfterNamed_ReportsE0008() {
        (_, DiagnosticBag bag) = CheckFirstCall("""s := input(prompt: "a", "b")""");

        AssertSingleError(bag, "E0008",
            "In the call to 'input', a positional argument follows a named argument. Move all named arguments after the positional ones.",
            25);
    }

    [Fact]
    public void InputCall_NamedTwice_ReportsE0010() {
        (_, DiagnosticBag bag) = CheckFirstCall("""s := input(prompt: "a", prompt: "b")""");

        AssertSingleError(bag, "E0010", "Parameter 'prompt' of 'input' is already supplied.", 25);
    }

    [Fact]
    public void InputCall_UnknownName_ReportsE0011() {
        (_, DiagnosticBag bag) = CheckFirstCall("""s := input(zzz: "a")""");

        AssertSingleError(bag, "E0011", "'input' has no parameter named 'zzz'.", 12);
    }

    [Fact]
    public void InputCall_CatalogueWithoutInput_IsAHostMisconfiguration() {
        // The checker registers input as a built-in name unconditionally and reads its
        // signature from the catalogue, so a catalogue without the io plugin is a host
        // error, not a diagnostic.
        var bag = new DiagnosticBag();
        CompilationUnit unit = Parser.Parse(Lexer.Scan("s := input()", bag), bag);

        GrobInternalException ex = Assert.Throws<GrobInternalException>(
            () => new TypeChecker(bag, NativeCatalogue.Empty).Check(unit));
        Assert.Contains("'input'", ex.Message);
    }

    [Fact]
    public void InputCall_NamedArgumentWrongType_ReportsE0004() {
        (_, DiagnosticBag bag) = CheckFirstCall("s := input(prompt: 1)");

        AssertSingleError(bag, "E0004",
            "Argument 1 to 'input' has type 'int', which is not assignable to parameter of type 'string'.",
            20);
    }

    // -----------------------------------------------------------------------
    // R-58 native widening: an int argument bound to a float parameter.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceCall_IntArgumentToFloatParameter_MarksTheSlotForWidening() {
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall("x := math.sqrt(4)");

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(0, null, WidenToFloat: true)], Slots(call));
    }

    [Fact]
    public void ScriptCall_IntArgumentToFloatParameter_MarksTheSlotForWidening() {
        (CallExpr call, DiagnosticBag bag) = CheckFirstCall("""
            x := half(3)
            fn half(v: float): float {
                return v / 2.0
            }
            """);

        Assert.False(bag.HasErrors);
        Assert.Equal([new BoundSlot(0, null, WidenToFloat: true)], Slots(call));
    }

    [Fact]
    public void ScriptCall_IntArgumentToIntParameter_IsNotWidened() {
        (CallExpr call, _) = CheckFirstCall("""
            x := twice(3)
            fn twice(v: int): int {
                return v * 2
            }
            """);

        Assert.Equal([new BoundSlot(0, null)], Slots(call));
    }
}
