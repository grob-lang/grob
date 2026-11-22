using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// The bound argument list the checker hands the compiler on
/// <see cref="CallExpr.ResolvedBinding"/> (native named arguments, gate report §1.5):
/// one slot per parameter, in parameter order, each either an argument of the call
/// or a default. The compiler emits the slots in order, so this record is what keeps
/// the checker and the compiler binding the same way (R-20).
/// </summary>
public sealed class TypeCheckerBoundArgumentListTests {
    private const string ScriptFn = """
        fn f(a: int, b: int = 99, c: bool = true): int {
            return a + b
        }

        """;

    private static CallExpr CheckLastCall(string source, out DiagnosticBag bag) {
        bag = new DiagnosticBag();
        CompilationUnit unit = Parser.Parse(Lexer.Scan(source, bag), bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
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

    private static int[] ArgumentIndices(CallExpr call) =>
        [.. call.ResolvedBinding!.Slots.Select(s => s.ArgumentIndex)];

    [Fact]
    public void ScriptCall_CompletePositional_BindsEachSlotToItsArgument() {
        CallExpr call = CheckLastCall(ScriptFn + "f(1, 2, false)", out DiagnosticBag bag);

        Assert.False(bag.HasErrors);
        Assert.Equal([0, 1, 2], ArgumentIndices(call));
    }

    [Fact]
    public void ScriptCall_NamedReorder_BindsSlotsInParameterOrder() {
        CallExpr call = CheckLastCall(ScriptFn + "f(1, c: false, b: 5)", out DiagnosticBag bag);

        Assert.False(bag.HasErrors);
        Assert.Equal([0, 2, 1], ArgumentIndices(call));
    }

    [Fact]
    public void ScriptCall_OmittedDefaults_LeavesDefaultSlotsWithNoNativeConstant() {
        CallExpr call = CheckLastCall(ScriptFn + "f(1, c: false)", out DiagnosticBag bag);

        Assert.False(bag.HasErrors);
        Assert.Equal([0, BoundSlot.DefaultArgument, 1], ArgumentIndices(call));
        // A script default is an expression compiled at the call site, not a constant.
        Assert.All(call.ResolvedBinding!.Slots, s => Assert.Null(s.NativeDefault));
    }

    [Fact]
    public void ScriptCall_BindingError_SetsNoRecord() {
        CallExpr call = CheckLastCall(ScriptFn + "f(1, z: 2)", out DiagnosticBag bag);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0011", diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
        Assert.Null(call.ResolvedBinding);
    }

    [Fact]
    public void ScriptCall_ArityError_SetsNoRecord() {
        CallExpr call = CheckLastCall(ScriptFn + "f()", out DiagnosticBag bag);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0003", diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
        Assert.Null(call.ResolvedBinding);
    }
}
