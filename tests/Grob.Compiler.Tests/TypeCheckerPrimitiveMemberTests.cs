using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for primitive instance-method/property dispatch (D-066's
/// compile-time-sugar model, proven on <c>string</c>) — the <c>ResolveMemberAccessCall</c>
/// call-position arm and the <c>VisitMemberAccess</c> bare-property arm, both driven by
/// <c>PrimitiveMemberRegistry</c>. Mirrors <c>TypeCheckerDateTests</c>' shape for the
/// equivalent <c>NamedTypeRegistry</c> dispatch.
/// </summary>
public sealed class TypeCheckerPrimitiveMemberTests {
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

    private static List<CallExpr> CollectCalls(CompilationUnit unit) {
        var collector = new CallCollector();
        collector.Visit(unit);
        return collector.Nodes;
    }

    private static List<MemberAccessExpr> CollectMemberAccesses(CompilationUnit unit) {
        var collector = new MemberAccessCollector();
        collector.Visit(unit);
        return collector.Nodes;
    }

    // -----------------------------------------------------------------------
    // Properties — s.length / s.isEmpty (bare access, VisitMemberAccess).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("length", GrobType.Int)]
    [InlineData("isEmpty", GrobType.Bool)]
    public void Property_ResolvesDeclaredType_AndNativeName(string property, GrobType expectedType) {
        var (unit, bag) = TypeCheckSource($$"""
            s := "abc"
            readonly v := s.{{property}}
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr node = Assert.Single(CollectMemberAccesses(unit), n => n.Member == property);
        Assert.Equal(expectedType, node.ResolvedFieldType);
        Assert.Equal($"string.{property}", node.ResolvedPrimitiveNativeName);
    }

    [Fact]
    public void UnknownBareProperty_ReportsSingleE1002() {
        DiagnosticBag bag = Check("""
            s := "abc"
            readonly v := s.nope
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void BareMethodName_NoParens_ReportsSingleE1002() {
        // A method name accessed without a call (s.trim, no parens) is not a property —
        // mirrors ResolveNamedTypePropertyAccess's identical un-called-method rejection.
        DiagnosticBag bag = Check("""
            s := "abc"
            readonly v := s.trim
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Methods — every no-default string member resolves its declared return type
    // and the qualified native name, with no diagnostics on a valid call.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("""s.toInt()""", GrobType.NullableInt, "string.toInt")]
    [InlineData("""s.toFloat()""", GrobType.NullableFloat, "string.toFloat")]
    [InlineData("""s.trim()""", GrobType.String, "string.trim")]
    [InlineData("""s.trimStart()""", GrobType.String, "string.trimStart")]
    [InlineData("""s.trimEnd()""", GrobType.String, "string.trimEnd")]
    [InlineData("""s.upper()""", GrobType.String, "string.upper")]
    [InlineData("""s.lower()""", GrobType.String, "string.lower")]
    [InlineData("""s.split(",")""", GrobType.Array, "string.split")]
    [InlineData("""s.contains("b")""", GrobType.Bool, "string.contains")]
    [InlineData("""s.startsWith("a")""", GrobType.Bool, "string.startsWith")]
    [InlineData("""s.endsWith("c")""", GrobType.Bool, "string.endsWith")]
    [InlineData("""s.replace("a", "z")""", GrobType.String, "string.replace")]
    [InlineData("""s.indexOf("b")""", GrobType.Int, "string.indexOf")]
    [InlineData("""s.lastIndexOf("b")""", GrobType.Int, "string.lastIndexOf")]
    [InlineData("""s.substring(0, 1)""", GrobType.String, "string.substring")]
    [InlineData("""s.repeat(3)""", GrobType.String, "string.repeat")]
    [InlineData("""s.left(1)""", GrobType.String, "string.left")]
    [InlineData("""s.right(1)""", GrobType.String, "string.right")]
    [InlineData("""s.toString()""", GrobType.String, "string.toString")]
    public void Method_ValidCall_ResolvesReturnTypeAndNativeName(
            string callExpr, GrobType expectedType, string expectedNative) {
        var (unit, bag) = TypeCheckSource($$"""
            s := "abc"
            readonly v := {{callExpr}}
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(expectedType, call.ResolvedReturnType);
        Assert.Equal(expectedNative, call.ResolvedPrimitiveNativeName);
    }

    [Fact]
    public void UnknownMethod_Call_ReportsSingleE1002() {
        DiagnosticBag bag = Check("""
            s := "abc"
            readonly v := s.nope()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void Contains_NoArguments_ReportsSingleE0003() {
        DiagnosticBag bag = Check("""
            s := "abc"
            readonly v := s.contains()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void Contains_WrongArgumentType_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""
            s := "abc"
            readonly v := s.contains(5)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(26, diag.Range.Start.Column);
    }

    [Fact]
    public void NamedArgument_OnPrimitiveMember_ReportsSingleE0011() {
        // The registry carries no parameter names, so a named argument cannot bind — and
        // emission preserves source order, which would silently mis-order a swapped pair.
        // Reject with E0011 (positional-only) rather than let it type-check as valid.
        DiagnosticBag bag = Check("""
            s := "abc"
            readonly v := s.repeat(count: 3)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0011.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(24, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Numeric-return-as-operand (D-362 threading) — CallExpr.ResolvedReturnType
    // must select the int arithmetic opcode downstream.
    // -----------------------------------------------------------------------

    [Fact]
    public void IndexOfResult_UsedAsArithmeticOperand_ResolvesReturnTypeInt() {
        var (unit, bag) = TypeCheckSource("""
            s := "abc"
            readonly n := s.indexOf("b") + 1
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        CallExpr call = Assert.Single(CollectCalls(unit));
        Assert.Equal(GrobType.Int, call.ResolvedReturnType);
    }

    [Fact]
    public void LengthProperty_UsedAsArithmeticOperand_ResolvesFieldTypeInt() {
        var (unit, bag) = TypeCheckSource("""
            s := "abc"
            readonly n := s.length * 2
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr node = Assert.Single(CollectMemberAccesses(unit), n => n.Member == "length");
        Assert.Equal(GrobType.Int, node.ResolvedFieldType);
    }

    // -----------------------------------------------------------------------
    // Nullable returns — toInt()/toFloat() are int?/float?.
    // -----------------------------------------------------------------------

    [Fact]
    public void ToInt_AssignedToNonNullableInt_ReportsSingleE0104() {
        DiagnosticBag bag = Check("""
            s := "42"
            n: int := 0
            n = s.toInt()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0104.Code, diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(5, diag.Range.Start.Column);
    }

    [Fact]
    public void ToInt_WithNullCoalesce_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            s := "42"
            readonly n := s.toInt() ?? 0
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // Regression — existing array/struct/date arms unaffected.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayHigherOrderMethod_Unchanged_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            arr := [1, 2, 3]
            arr.select(x => x)
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void NamedTypeMethodCall_Unchanged_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly s := d.toIso()
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }
}
