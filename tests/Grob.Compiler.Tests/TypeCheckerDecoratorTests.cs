using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Static validation of <c>param</c> decorators (D-424 Decision 3) — E4001,
/// E4002, E4101 and E4102's first throw sites. Every one of §19's seven
/// decorators shipped unvalidated before D-424, because decorators were not in
/// the AST at all.
/// <para>
/// <b>Static validation only.</b> Everything here is checked with no parameter
/// value in hand: the decorator's name, its arity, its arguments' literal kind,
/// duplicate application, and applicability to the param's declared type.
/// <i>Enforcing</i> a constraint — rejecting a supplied <c>threshold</c> of 150
/// against <c>@maxValue(100)</c> — needs a bound parameter and is Sprint 10
/// (R-15). A statically valid decorator therefore compiles clean here and
/// applies no constraint to any value yet; that is the intended end state, not
/// an omission.
/// </para>
/// </summary>
public sealed class TypeCheckerDecoratorTests {
    private static DiagnosticBag Check(string source, out CompilationUnit unit) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        Assert.Empty(bag.Diagnostics);
        unit = Parser.Parse(tokens, bag);
        Assert.Empty(bag.Diagnostics);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    private static DiagnosticBag Check(string source) => Check(source, out _);

    /// <summary>
    /// Asserts the one diagnostic, its position and a fragment of its wording.
    /// The fragment matters: E4002 carries four distinct conditions — wrong
    /// target, wrong param type for the decorator, duplicate application, and a
    /// decorator outside a <c>param</c> declaration — and E4102 four decorators.
    /// The code is shared, the wording is not, so a test that pinned the code
    /// alone would pass against the wrong message.
    /// </summary>
    private static void AssertSingle(
            DiagnosticBag bag, string code, int line, int column, string messageFragment) {
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal(code, d.Code);
        Assert.Equal(line, d.Range.Start.Line);
        Assert.Equal(column, d.Range.Start.Column);
        Assert.Contains(messageFragment, d.Message, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // One positive and one negative per decorator, all seven.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("@secure\nparam token: string\n")]
    [InlineData("@allowed(\"dev\", \"staging\", \"prod\")\nparam environment: string\n")]
    [InlineData("@allowed(1, 2, 3)\nparam mode: int\n")]
    [InlineData("@minLength(1)\nparam name: string\n")]
    [InlineData("@maxLength(64)\nparam name: string\n")]
    [InlineData("@minLength(1)\nparam names: string[]\n")]
    [InlineData("@minValue(0)\nparam threshold: int\n")]
    [InlineData("@maxValue(100)\nparam threshold: int\n")]
    [InlineData("@minValue(-10)\nparam offset: int\n")]
    [InlineData("@maxValue(1.5)\nparam ratio: float\n")]
    [InlineData("@pattern(\"^[a-z]+$\")\nparam slug: string\n")]
    // The canonical §19 stack: two constraints of the same family are the
    // ordinary case, not a duplicate.
    [InlineData("@minValue(0)\n@maxValue(100)\nparam threshold: int = 80\n")]
    [InlineData("@minLength(1)\n@maxLength(64)\nparam name: string\n")]
    [InlineData("@secure\n@minLength(32)\nparam token: string\n")]
    public void ValidDecorator_TypeChecksClean(string source) {
        DiagnosticBag bag = Check(source);
        Assert.Empty(bag.Diagnostics);
    }

    // -----------------------------------------------------------------------
    // E4001 — the decorator name is not one of D-411's seven. The set does not
    // grow by usage and there is no user-defined decorator mechanism in v1.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("@bogus\nparam x: int = 1\n", "bogus")]
    [InlineData("@admin\nparam user: string\n", "admin")]
    [InlineData("@Secure\nparam token: string\n", "Secure")] // case-sensitive: not @secure
    [InlineData("@required(1)\nparam x: int = 1\n", "required")]
    public void UnknownDecorator_IsE4001(string source, string name) {
        DiagnosticBag bag = Check(source);
        AssertSingle(bag, "E4001", 1, 1, $"'@{name}' is not a recognised decorator");
    }

    /// <summary>
    /// D-424 Decision 4: <c>@pattern</c> is in the v1 set (D-411), so it is not
    /// E4001. Treating it as unknown until the <c>regex</c> increment lands would
    /// make a specified v1 decorator an error for one sprint and then silently
    /// stop being one.
    /// </summary>
    [Fact]
    public void Pattern_IsRecognised_NotE4001() {
        DiagnosticBag bag = Check("@pattern(\"^v[0-9]+$\")\nparam tag: string\n");
        Assert.Empty(bag.Diagnostics);
    }

    /// <summary>
    /// And the deferral itself, pinned so the <c>regex</c> increment finds a test
    /// to change rather than a silent gap: <c>@pattern</c>'s argument is checked
    /// for being a string literal and nothing more. This pattern cannot compile —
    /// the bracket is never closed — and it is accepted anyway.
    /// </summary>
    [Fact]
    public void PatternArgument_IsNotValidatedAsARegex_Deferred() {
        DiagnosticBag bag = Check("@pattern(\"[unclosed\")\nparam tag: string\n");
        Assert.Empty(bag.Diagnostics);
    }

    // -----------------------------------------------------------------------
    // E4002 — duplicate application. §19: `@minLength` with `@maxLength`, or
    // `@minValue` with `@maxValue`, is the ordinary case and is not a duplicate.
    // -----------------------------------------------------------------------

    [Fact]
    public void SameDecoratorTwice_IsE4002AtTheSecond() {
        DiagnosticBag bag = Check("@secure\n@secure\nparam token: string\n");
        AssertSingle(bag, "E4002", 2, 1, "'@secure' is already applied to 'token' on line 1");
    }

    [Fact]
    public void SameDecoratorTwiceWithDifferentArguments_IsStillE4002() {
        DiagnosticBag bag = Check("@minLength(1)\n@minLength(2)\nparam name: string\n");
        AssertSingle(bag, "E4002", 2, 1, "'@minLength' is already applied to 'name' on line 1");
    }

    // -----------------------------------------------------------------------
    // E4002 — wrong param type for the decorator.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("@secure\nparam x: int = 1\n", "'@secure' applies to a 'string' parameter")]
    [InlineData("@secure\nparam x: bool = true\n", "'@secure' applies to a 'string' parameter")]
    [InlineData("@secure\nparam x: float = 1.0\n", "'@secure' applies to a 'string' parameter")]
    [InlineData("@secure\nparam x: int[]\n", "'@secure' applies to a 'string' parameter")]
    [InlineData("@pattern(\"a\")\nparam x: int = 1\n", "'@pattern' applies to a 'string' parameter")]
    public void DecoratorOnTheWrongParamType_IsE4002(string source, string fragment) {
        DiagnosticBag bag = Check(source);
        AssertSingle(bag, "E4002", 1, 1, fragment);
    }

    [Fact]
    public void SecureOnANullableString_IsAccepted() {
        DiagnosticBag bag = Check("@secure\nparam token: string?\n");
        Assert.Empty(bag.Diagnostics);
    }

    // -----------------------------------------------------------------------
    // E4002 — arity, for the two decorators §19's table leaves without a code of
    // their own. E4101 names `@allowed`; E4102 names the four length and value
    // constraints; neither can carry `@secure` or `@pattern`, and D-424 mints no
    // new code, so the shared "decorator not permitted here" carries them with
    // its own wording.
    // -----------------------------------------------------------------------

    [Fact]
    public void SecureWithAnArgument_IsE4002() {
        DiagnosticBag bag = Check("@secure(1)\nparam token: string\n");
        AssertSingle(bag, "E4002", 1, 1, "'@secure' takes no arguments");
    }

    [Theory]
    [InlineData("@pattern()\nparam tag: string\n")]
    [InlineData("@pattern(\"a\", \"b\")\nparam tag: string\n")]
    public void PatternWithWrongArity_IsE4002(string source) {
        DiagnosticBag bag = Check(source);
        AssertSingle(bag, "E4002", 1, 1, "'@pattern' takes exactly one string literal");
    }

    [Fact]
    public void PatternWithANonStringLiteral_IsE4002AtTheArgument() {
        DiagnosticBag bag = Check("@pattern(3)\nparam tag: string\n");
        AssertSingle(bag, "E4002", 1, 10, "'@pattern' needs a string literal");
    }

    // -----------------------------------------------------------------------
    // E4101 — `@allowed`.
    // -----------------------------------------------------------------------

    [Fact]
    public void AllowedWithNoValues_IsE4101() {
        DiagnosticBag bag = Check("@allowed()\nparam environment: string\n");
        AssertSingle(bag, "E4101", 1, 1, "at least one permitted value");
    }

    /// <summary>
    /// The diagnostic lands at the argument's own position, which is why
    /// decorator arguments are parsed as ordinary expressions rather than
    /// restricted to literals at the parse layer (D-424 Decision 1).
    /// </summary>
    [Fact]
    public void AllowedWithANonLiteralValue_IsE4101AtTheArgument() {
        DiagnosticBag bag = Check("@allowed(names)\nparam environment: string\nconst names := 1\n");

        // The identifier is undefined here as well; the ordering rule makes it
        // unavoidable, since a `const` may never precede a `param`. The
        // decorator diagnostic is the one under test.
        Diagnostic d = Assert.Single(bag.Diagnostics, x => x.Code == "E4101");
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
    }

    [Fact]
    public void AllowedWithHeterogeneousValues_IsE4101AtTheOffendingValue() {
        DiagnosticBag bag = Check("@allowed(1, \"two\")\nparam mode: int\n");
        AssertSingle(bag, "E4101", 1, 13, "must all be the same type");
    }

    [Fact]
    public void AllowedValueNotAssignableToTheParamType_IsE4101AtTheValue() {
        DiagnosticBag bag = Check("@allowed(\"dev\")\nparam mode: int\n");
        AssertSingle(bag, "E4101", 1, 10, "is not assignable to 'mode'");
    }

    // -----------------------------------------------------------------------
    // E4102 — `@minLength`, `@maxLength`, `@minValue`, `@maxValue`.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("@minLength()\nparam name: string\n",
        "'@minLength' takes exactly one non-negative integer literal")]
    [InlineData("@maxLength(1, 2)\nparam name: string\n",
        "'@maxLength' takes exactly one non-negative integer literal")]
    [InlineData("@minValue()\nparam n: int\n", "'@minValue' takes exactly one numeric literal")]
    [InlineData("@maxValue(1, 2)\nparam n: int\n", "'@maxValue' takes exactly one numeric literal")]
    public void LengthOrValueConstraintWithWrongArity_IsE4102(string source, string fragment) {
        DiagnosticBag bag = Check(source);
        AssertSingle(bag, "E4102", 1, 1, fragment);
    }

    [Fact]
    public void MinLengthWithANonIntegerLiteral_IsE4102AtTheArgument() {
        DiagnosticBag bag = Check("@minLength(\"x\")\nparam name: string\n");
        AssertSingle(bag, "E4102", 1, 12, "'@minLength' needs a non-negative integer literal.");
    }

    [Fact]
    public void MinLengthWithANegativeLiteral_IsE4102AtTheArgument() {
        DiagnosticBag bag = Check("@minLength(-1)\nparam name: string\n");
        AssertSingle(bag, "E4102", 1, 12, "a length is never negative");
    }

    [Fact]
    public void MinLengthOnANumericParam_IsE4102() {
        DiagnosticBag bag = Check("@minLength(1)\nparam n: int\n");
        AssertSingle(bag, "E4102", 1, 1, "'@minLength' applies to a 'string' or an array parameter");
    }

    [Fact]
    public void MinValueWithANonNumericLiteral_IsE4102AtTheArgument() {
        DiagnosticBag bag = Check("@minValue(\"x\")\nparam n: int\n");
        AssertSingle(bag, "E4102", 1, 11, "'@minValue' needs a numeric literal");
    }

    [Fact]
    public void MaxValueOnAStringParam_IsE4102() {
        DiagnosticBag bag = Check("@maxValue(1)\nparam name: string\n");
        AssertSingle(bag, "E4102", 1, 1, "'@maxValue' applies to an 'int' or a 'float' parameter");
    }

    [Fact]
    public void MinLengthWithANonLiteralArgument_IsE4102AtTheArgument() {
        DiagnosticBag bag = Check("@minLength(n)\nparam name: string\n");

        Diagnostic d = Assert.Single(bag.Diagnostics, x => x.Code == "E4102");
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(12, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 — the checker visits decorator arguments, so every identifier
    // appearing in one carries a non-null ResolvedType and Declaration after
    // type-check, with D-311's sentinels on the error path. This is the same
    // reasoning that already makes the checker visit a `param` default.
    // -----------------------------------------------------------------------

    [Fact]
    public void IdentifierInADecoratorArgument_CarriesTheErrorSentinels() {
        DiagnosticBag bag = Check("@allowed(missing)\nparam environment: string\n", out CompilationUnit unit);

        Assert.Contains(bag.Diagnostics, d => d.Code == "E1001");

        ParamDecl p = Assert.IsType<ParamDecl>(unit.TopLevel[0]);
        Decorator decorator = Assert.Single(p.Decorators);
        IdentifierExpr argument = Assert.IsType<IdentifierExpr>(Assert.Single(decorator.Arguments));

        Assert.Equal(GrobType.Error, argument.ResolvedType);
        Assert.Same(UnresolvedDecl.Instance, argument.Declaration);
    }

    /// <summary>
    /// The invariant holds under an unknown decorator too — the arguments are
    /// visited before the name is judged, so an E4001 never leaves an identifier
    /// node with unset fields.
    /// </summary>
    [Fact]
    public void IdentifierInAnUnknownDecoratorsArgument_StillCarriesTheSentinels() {
        DiagnosticBag bag = Check("@bogus(missing)\nparam environment: string\n", out CompilationUnit unit);

        Assert.Contains(bag.Diagnostics, d => d.Code == "E4001");
        Assert.Contains(bag.Diagnostics, d => d.Code == "E1001");

        ParamDecl p = Assert.IsType<ParamDecl>(unit.TopLevel[0]);
        IdentifierExpr argument =
            Assert.IsType<IdentifierExpr>(Assert.Single(Assert.Single(p.Decorators).Arguments));

        Assert.Equal(GrobType.Error, argument.ResolvedType);
        Assert.Same(UnresolvedDecl.Instance, argument.Declaration);
    }

    // -----------------------------------------------------------------------
    // Two-mode (D-039): a decorator error suppresses nothing else, and the
    // diagnostics come out in source order.
    // -----------------------------------------------------------------------

    [Fact]
    public void SeveralDecoratorErrorsAcrossAStack_AreAllReportedInSourceOrder() {
        DiagnosticBag bag = Check(
            "@bogus\n" +
            "@minLength(\"x\")\n" +
            "@minLength(2)\n" +
            "param name: string\n");

        Assert.Equal(["E4001", "E4102", "E4002"], bag.Diagnostics.Select(d => d.Code));
        Assert.Equal([1, 2, 3], bag.Diagnostics.Select(d => d.Range.Start.Line));
    }

    /// <summary>
    /// A decorator error on a <c>param</c> that is also misordered reports both:
    /// D-412's cascade suppresses only the name collision, which has the same
    /// root cause as the ordering error. A bad decorator does not.
    /// </summary>
    [Fact]
    public void MisorderedParamWithABadDecorator_ReportsBoth() {
        DiagnosticBag bag = Check("x := 1\n@bogus\nparam name: string\n");

        Assert.Equal(["E2202", "E4001"], bag.Diagnostics.Select(d => d.Code));
    }
}
