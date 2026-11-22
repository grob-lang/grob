using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Change group 3 of the native named arguments increment: primitive methods bind named
/// arguments on the shared binder (D-446 Decisions 1 and 2). The convention and its four
/// diagnostics are the script-function ones with the member named as its arity messages
/// name it (<c>'padLeft'</c>, gate report §1.4). <c>padLeft</c> is the real shape to
/// test against: a required parameter and one default. No first-party primitive has two
/// defaults or a float parameter, and the registrar that could add one is sealed off by a
/// guard test, so skip and fill on this path is proven in
/// <see cref="CompilerPrimitiveNamedArgumentTests"/> by a hand-built call.
/// </summary>
public sealed class TypeCheckerPrimitiveNamedArgumentTests {
    private static (CallExpr Call, DiagnosticBag Bag) CheckCall(string call) {
        var bag = new DiagnosticBag();
        CompilationUnit unit = Parser.Parse(Lexer.Scan($"s := \"abc\"\nv := {call}", bag), bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
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

    // -----------------------------------------------------------------------
    // Binding: a named argument binds; a default is filled.
    // -----------------------------------------------------------------------

    [Fact]
    public void PrimitiveCall_NamedDefaultedParameter_Binds() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("""s.padLeft(5, char: "*")""");

        Assert.False(bag.HasErrors);
        Assert.Equal("string.padLeft", call.ResolvedPrimitiveNativeName);
        Assert.Equal([new BoundSlot(0, null), new BoundSlot(1, null)], Slots(call));
    }

    [Fact]
    public void PrimitiveCall_OmittedDefault_FillsItWithItsConstant() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("s.padLeft(5)");

        Assert.False(bag.HasErrors);
        Assert.Equal(
            [new BoundSlot(0, null), new BoundSlot(BoundSlot.DefaultArgument, GrobValue.FromString(" "))],
            Slots(call));
    }

    [Fact]
    public void PrimitiveCall_NoParameters_BindsAnEmptyList() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("s.trim()");

        Assert.False(bag.HasErrors);
        Assert.Empty(Slots(call));
    }

    // -----------------------------------------------------------------------
    // The four binding diagnostics, member wording (gate report §1.4).
    // -----------------------------------------------------------------------

    [Fact]
    public void PrimitiveCall_PositionalAfterNamed_ReportsE0008() {
        (CallExpr call, DiagnosticBag bag) = CheckCall("""s.padLeft(char: "*", 5)""");

        AssertSingleError(bag, "E0008",
            "In the call to 'padLeft', a positional argument follows a named argument. Move all named arguments after the positional ones.",
            27);
        Assert.Null(call.ResolvedBinding);
    }

    [Fact]
    public void PrimitiveCall_NamedRequiredParameter_ReportsE0009() {
        (_, DiagnosticBag bag) = CheckCall("s.padLeft(width: 5)");

        AssertSingleError(bag, "E0009",
            "Parameter 'width' of 'padLeft' is required and has no default, so it cannot be passed by name. Pass it positionally.",
            16);
    }

    [Fact]
    public void PrimitiveCall_NamedTwice_ReportsE0010() {
        (_, DiagnosticBag bag) = CheckCall("""s.padLeft(5, char: "a", char: "b")""");

        AssertSingleError(bag, "E0010", "Parameter 'char' of 'padLeft' is already supplied.", 30);
    }

    [Fact]
    public void PrimitiveCall_UnknownName_ReportsE0011() {
        (_, DiagnosticBag bag) = CheckCall("""s.padLeft(5, zzz: "a")""");

        AssertSingleError(bag, "E0011", "'padLeft' has no parameter named 'zzz'.", 19);
    }

    [Fact]
    public void PrimitiveCall_BindingError_SuppressesArityAndTypeChecks() {
        // zzz: 1 is unknown, so width is also unbound (E0003) and 1 would be a type
        // question; one diagnostic per root cause.
        (CallExpr call, DiagnosticBag bag) = CheckCall("s.padLeft(zzz: 1)");

        AssertSingleError(bag, "E0011", "'padLeft' has no parameter named 'zzz'.", 16);
        Assert.Null(call.ResolvedBinding);
    }

    // -----------------------------------------------------------------------
    // Arity (E0003) and type (E0004) on the bound set.
    // -----------------------------------------------------------------------

    [Fact]
    public void PrimitiveCall_NamedDefaultWithRequiredMissing_ReportsE0003AtTheCall() {
        (_, DiagnosticBag bag) = CheckCall("""s.padLeft(char: "*")""");

        AssertSingleError(bag, "E0003", "'padLeft' expects between 1 and 2 arguments, but 1 was supplied.", 6);
    }

    [Fact]
    public void PrimitiveCall_TooManyPositionals_ReportsE0003() {
        (_, DiagnosticBag bag) = CheckCall("""s.padLeft(5, "x", "y")""");

        AssertSingleError(bag, "E0003", "'padLeft' expects between 1 and 2 arguments, but 3 were supplied.", 6);
    }

    [Fact]
    public void PrimitiveCall_NamedArgumentWrongType_ReportsE0004AtTheArgument() {
        (_, DiagnosticBag bag) = CheckCall("s.padLeft(5, char: 9)");

        AssertSingleError(bag, "E0004",
            "Argument to 'padLeft' has type 'int', which is not assignable to parameter of type 'string'.",
            25);
    }

    [Fact]
    public void PrimitiveCall_UnknownArgument_IsNotReportedAsAMismatch() {
        // x has no shape, so its type is Unknown, which is assignable to anything.
        var lambdaBag = new DiagnosticBag();
        CompilationUnit unit = Parser.Parse(Lexer.Scan("lam := x => \"abc\".padLeft(x)", lambdaBag), lambdaBag);
        new TypeChecker(lambdaBag, SharedCatalogue.Stdlib).Check(unit);

        Assert.False(lambdaBag.HasErrors, string.Join("; ", lambdaBag.Errors.Select(d => d.Message)));
    }

    [Fact]
    public void PrimitiveCall_ErroredArgument_ReportsOnlyTheRootCause() {
        (_, DiagnosticBag bag) = CheckCall("s.padLeft(missing)");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
    }

    // -----------------------------------------------------------------------
    // A nullable receiver binds the same way, and keeps the guarded path.
    // -----------------------------------------------------------------------

    [Fact]
    public void NullablePrimitiveCall_NamedDefault_BindsAndTakesTheGuardedPath() {
        var bag = new DiagnosticBag();
        CompilationUnit unit = Parser.Parse(
            Lexer.Scan("""
                fn pad(ns: string?): string? {
                    return ns?.padLeft(5, char: "*")
                }
                """, bag), bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);

        Assert.False(bag.HasErrors, string.Join("; ", bag.Errors.Select(d => d.Message)));
        CallExpr call = FindLastCall(unit);
        Assert.Null(call.ResolvedPrimitiveNativeName);
        Assert.Equal([new BoundSlot(0, null), new BoundSlot(1, null)], Slots(call));
    }
}
