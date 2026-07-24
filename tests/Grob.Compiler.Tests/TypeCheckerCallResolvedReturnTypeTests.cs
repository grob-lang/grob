using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for the <c>GetExprType</c> arithmetic-operand completeness sweep
/// (D-362, closing D-360's residue). <see cref="CallExpr.ResolvedReturnType"/> is a new
/// node field, mirroring <see cref="IndexExpr.ElementType"/> (D-359) and
/// <see cref="MemberAccessExpr.ResolvedFieldType"/>, populated by the type checker so the
/// compiler's <c>GetExprType</c> can read a call's return type without re-deriving it.
/// Covers the three real sub-cases D-360 catalogued (native/stdlib, function-typed
/// variable, nominal-type method) plus confirmation that the genuinely unresolvable
/// residue (a void-returning array higher-order call) stays <see cref="GrobType.Unknown"/>.
/// </summary>
public sealed class TypeCheckerCallResolvedReturnTypeTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static string FormatErrors(DiagnosticBag bag) =>
        string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"));

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
    // Sub-case 1: native/stdlib call (math.sqrt) — previously fell to Unknown
    // because GetExprType had no MemberAccessExpr-callee CallExpr arm at all.
    // -----------------------------------------------------------------------

    [Fact]
    public void NativeCall_MathSqrt_AnnotatesFloatReturnType() {
        var (unit, bag) = TypeCheckSource("x := math.sqrt(4.0)\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Float, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // Sub-case 2: function-typed-variable call (fnVar()) — previously fell to
    // Unknown because VisitCall only resolves a return type when Declaration
    // is literally an FnDecl, not a value binding holding a function.
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionTypedVariableCall_AnnotatesDeclaredReturnType() {
        var (unit, bag) = TypeCheckSource(
            "f: fn(): float := () => 2.5\n" +
            "x := f()\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit), c => c.Callee is IdentifierExpr { Name: "f" });
        Assert.Equal(GrobType.Float, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // Sub-case 3: nominal-type-method call (date.daysUntil) via NamedTypeRegistry
    // (D-361/C0c) — previously fell to Unknown because ValidateNamedTypeMethodCall
    // never persisted the resolved return type anywhere the compiler could reach.
    // -----------------------------------------------------------------------

    [Fact]
    public void NominalTypeMethodCall_DaysUntil_AnnotatesIntReturnType() {
        var (unit, bag) = TypeCheckSource(
            "a := date.of(2026, 1, 1)\n" +
            "b := date.of(2026, 1, 11)\n" +
            "x := a.daysUntil(b)\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit), c => c.Callee is MemberAccessExpr { Member: "daysUntil" });
        Assert.Equal(GrobType.Int, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // Residue: a void-returning array higher-order call (arr.each(...)) stays
    // Unknown — genuinely unresolvable, not an accidental gap this sweep closes.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayEachCall_StaysUnknownReturnType() {
        var (unit, bag) = TypeCheckSource(
            "arr := [1, 2, 3]\n" +
            "arr.each((v) => v)\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit), c => c.Callee is MemberAccessExpr { Member: "each" });
        Assert.Equal(GrobType.Unknown, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // String-returning built-ins (CodeRabbit PR #152): input() and formatAs
    // resolve to String at type-check but return through their own dedicated
    // paths (CheckInputCall / ResolveFormatAsCall) that short-circuit before
    // VisitCall's general ResolvedReturnType assignment. Without persisting
    // String here, GetExprType reads the default Unknown, so a string-concat
    // operand (input() + "x") mis-selects the arithmetic opcode instead of
    // Concat — the exact bug class D-362 set out to close.
    // -----------------------------------------------------------------------

    [Fact]
    public void InputCall_AnnotatesStringReturnType() {
        var (unit, bag) = TypeCheckSource("x := input()\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit), c => c.Callee is IdentifierExpr { Name: "input" });
        Assert.Equal(GrobType.String, call.ResolvedReturnType);
    }

    [Fact]
    public void FormatAsCall_AnnotatesStringReturnType() {
        var (unit, bag) = TypeCheckSource(
            "type Item {\n" +
            "    name: string\n" +
            "    price: float\n" +
            "}\n" +
            "fn report(items: Item[]): string {\n" +
            "    return items.formatAs.table()\n" +
            "}\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit), c => c.Callee is MemberAccessExpr { Member: "table" });
        Assert.Equal(GrobType.String, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // Sprint 9 Increment A1b (D-370): the int/float type-static namespace-native
    // calls route through the same generic ResolveNamespaceMemberCall arm as
    // math.sqrt above — no code change needed, confirmed by these sibling cases.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamespaceNative_IntMax_AnnotatesIntReturnType() {
        var (unit, bag) = TypeCheckSource("x := int.max(1, 2)\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Int, call.ResolvedReturnType);
    }

    [Fact]
    public void NamespaceNative_FloatClamp_AnnotatesFloatReturnType() {
        var (unit, bag) = TypeCheckSource("x := float.clamp(1.5, 0.0, 1.0)\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Float, call.ResolvedReturnType);
    }

    // -----------------------------------------------------------------------
    // Nominal-self-returning method (date.addDays returns date): resolves to
    // Struct via the ReturnsNominalSelf arm of ResolveNamedTypeMethodCall.
    // Complements the daysUntil (int) sub-case above so both nominal-method
    // return arms — declared-type and nominal-self — are exercised.
    // -----------------------------------------------------------------------

    [Fact]
    public void NominalSelfReturningMethodCall_AddDays_AnnotatesStructReturnType() {
        var (unit, bag) = TypeCheckSource(
            "a := date.of(2026, 1, 1)\n" +
            "b := a.addDays(10)\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit), c => c.Callee is MemberAccessExpr { Member: "addDays" });
        Assert.Equal(GrobType.Struct, call.ResolvedReturnType);
    }
}
