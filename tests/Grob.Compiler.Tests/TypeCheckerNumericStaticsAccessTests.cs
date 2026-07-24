using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 9 Increment A1b (D-370)'s <c>int</c>/<c>float</c>
/// type-static namespace registration in <c>NamespaceRegistry</c> — arity/type
/// validation against the compile-time twin, mirroring how <c>math</c>/<c>env</c>
/// namespace calls are already checked (<see cref="TypeCheckerNamespaceAccessTests"/>,
/// <see cref="TypeCheckerEnvAccessTests"/>). Completes the numeric surface D-369 began
/// (the instance-method surface, <c>int.abs()</c>/<c>float.roundTo()</c>) — these six
/// are namespace-receiver calls, not instance methods.
/// </summary>
public sealed class TypeCheckerNumericStaticsAccessTests {
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

    private sealed class MemberAccessCollector : AstWalker {
        public List<MemberAccessExpr> Nodes { get; } = [];
        public override Unit VisitMemberAccess(MemberAccessExpr node) {
            Nodes.Add(node);
            Visit(node.Target);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static IReadOnlyList<MemberAccessExpr> CollectMemberAccesses(CompilationUnit unit) {
        MemberAccessCollector collector = new();
        collector.Visit(unit);
        return collector.Nodes;
    }

    // -----------------------------------------------------------------------
    // Valid calls — each of the six resolves to its declared return type,
    // no diagnostics.
    // -----------------------------------------------------------------------

    [Fact]
    public void IntMin_ValidArgs_ResolvesToInt_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("readonly x := int.min(1, 2)");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Int, ma.ResolvedFieldType);
    }

    [Fact]
    public void IntMax_ValidArgs_ResolvesToInt_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("readonly x := int.max(1, 2)");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Int, ma.ResolvedFieldType);
    }

    [Fact]
    public void IntClamp_ValidArgs_ResolvesToInt_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("readonly x := int.clamp(150, 0, 100)");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Int, ma.ResolvedFieldType);
    }

    [Fact]
    public void FloatMin_ValidArgs_ResolvesToFloat_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("readonly x := float.min(1.0, 2.0)");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Float, ma.ResolvedFieldType);
    }

    [Fact]
    public void FloatMax_ValidArgs_ResolvesToFloat_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("readonly x := float.max(1.0, 2.0)");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Float, ma.ResolvedFieldType);
    }

    [Fact]
    public void FloatClamp_ValidArgs_ResolvesToFloat_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("readonly x := float.clamp(1.5, 0.0, 1.0)");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Float, ma.ResolvedFieldType);
    }

    // -----------------------------------------------------------------------
    // int -> float widening — the one implicit widening in the language. A
    // float-typed native accepts an int argument (correcting the archived
    // increment prompt's own miswritten "narrowing" example).
    // -----------------------------------------------------------------------

    [Fact]
    public void FloatMax_IntAndFloatArgs_WidensToFloat_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly x := float.max(1, 2.0)");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // Genuine argument-type mismatches — E0004. int.min(1.0, 2): a float
    // argument to an int parameter does not narrow. float.max(1, "x"): a
    // string argument to a float parameter is never assignable.
    // -----------------------------------------------------------------------

    [Fact]
    public void IntMin_FloatArg_ReportsSingleE0004() {
        DiagnosticBag bag = Check("readonly x := int.min(1.0, 2)");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(23, diag.Range.Start.Column);
    }

    [Fact]
    public void FloatMax_StringArg_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""readonly x := float.max(1, "x")""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(28, diag.Range.Start.Column);
    }

    [Fact]
    public void IntClamp_StringFirstArg_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""readonly x := int.clamp("x", 0, 10)""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(25, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Wrong arity — E0003, at the call's own range (column 15 in every case
    // here: "readonly x := " is 14 characters regardless of which of the six
    // functions is called, so the receiver always starts at column 15).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("readonly x := int.min(1)")]
    [InlineData("readonly x := int.max(1, 2, 3)")]
    [InlineData("readonly x := int.clamp(1, 2)")]
    [InlineData("readonly x := float.min(1.0)")]
    [InlineData("readonly x := float.max(1.0, 2.0, 3.0)")]
    [InlineData("readonly x := float.clamp(1.0, 2.0)")]
    public void WrongArity_ReportsSingleE0003AtCallStart(string source) {
        DiagnosticBag bag = Check(source);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Shadowing consequence — int/float are now pre-registered NamespaceDecl
    // symbols in the global scope, so a top-level ':=' binding of the same
    // name collides exactly as 'math := 5' already does (VisitVarDecl's
    // same-scope check runs before the reserved-identifier special case, and
    // int/float are deliberately NOT added to _reservedIdentifiers — only
    // 'formatAs'/'select' are reserved, for the separate D-320 reason).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("int := 5")]
    [InlineData("float := 5")]
    public void TypeName_AsTopLevelBindingName_ReportsSingleE1102(string source) {
        DiagnosticBag bag = Check(source + "\n");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1102.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void MathAsTopLevelBindingName_ReportsSingleE1102_ConfirmsThePrecedent() {
        // The precedent this increment's shadowing behaviour follows: 'math' is an
        // existing namespace and already collides the same way.
        DiagnosticBag bag = Check("math := 5\n");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1102.Code, diag.Code);
    }

    // -----------------------------------------------------------------------
    // Annotation-position regression — the most important regression this
    // increment could introduce. 'x: int' resolves through ResolveSignatureType's
    // literal string mapping, not symbol lookup, so registering 'int'/'float' as
    // namespace symbols must not disturb it.
    // -----------------------------------------------------------------------

    [Fact]
    public void VariableAnnotation_Int_StillResolvesToIntType() {
        var (unit, bag) = TypeCheckSource("x: int := 5\n");

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void StructField_TypedInt_StillResolvesCorrectly() {
        var (unit, bag) = TypeCheckSource("""
            type Point {
                x: int
                y: int
            }
            readonly p := Point { x: 1, y: 2 }
            readonly sum := p.x + p.y
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr xAccess = Assert.Single(CollectMemberAccesses(unit), m => m.Member == "x");
        Assert.Equal(GrobType.Int, xAccess.ResolvedFieldType);
    }

    [Fact]
    public void FunctionReturnType_Float_StillResolvesCorrectly() {
        var (unit, bag) = TypeCheckSource("""
            fn half(n: float): float {
                return n / 2.0
            }
            readonly r := half(10.0)
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }
}
