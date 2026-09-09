using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser tests for the <see cref="Decorator"/> node (D-424 Decision 1). Before
/// D-424 a decorator stack was scanned and discarded by <c>SkipParameterDecorators</c>
/// and nothing downstream could see one; it is now captured onto
/// <see cref="ParamDecl.Decorators"/>, and the declaration's
/// <see cref="AstNode.Range"/> extends over the stack so a diagnostic about a
/// decorator can point at the declaration it belongs to.
/// <para>
/// Arguments are parsed as ordinary expressions, deliberately (D-424 Decision 1):
/// the restriction to literals is real but is enforced by the type checker, so
/// <c>@minLength(x)</c> yields a semantic diagnostic at the argument's own
/// position rather than a parse error, and §29 recovery treats a malformed
/// decorator like any other malformed construct.
/// </para>
/// </summary>
public sealed class ParserDecoratorTests {
    /// <summary>
    /// A double-quoted string literal parses to an <see cref="InterpolatedStringExpr"/>
    /// carrying one <see cref="StringTextPart"/>, not to a
    /// <c>StringLiteralExpr</c> — the interpolation-capable form is the one the
    /// lexer/parser produce for <c>"…"</c>.
    /// </summary>
    private static string TextOf(Expression expression) {
        InterpolatedStringExpr str = Assert.IsType<InterpolatedStringExpr>(expression);
        return Assert.IsType<StringTextPart>(Assert.Single(str.Parts)).Text;
    }

    // -----------------------------------------------------------------------
    // Capture
    // -----------------------------------------------------------------------

    [Fact]
    public void SingleDecorator_IsCapturedOntoTheDeclaration() {
        CompilationUnit unit = ParseOk("@secure\nparam token: string\n");
        ParamDecl p = Single<ParamDecl>(unit);

        Decorator d = Assert.Single(p.Decorators);
        Assert.Equal("secure", d.Name);
        Assert.Empty(d.Arguments);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
    }

    [Fact]
    public void DecoratorStack_IsCapturedInSourceOrder() {
        CompilationUnit unit = ParseOk(
            "@minValue(0)\n@maxValue(100)\nparam threshold: int = 80\n");
        ParamDecl p = Single<ParamDecl>(unit);

        Assert.Equal(2, p.Decorators.Count);
        Assert.Equal("minValue", p.Decorators[0].Name);
        Assert.Equal("maxValue", p.Decorators[1].Name);
        Assert.Equal(1, p.Decorators[0].Range.Start.Line);
        Assert.Equal(2, p.Decorators[1].Range.Start.Line);
    }

    [Fact]
    public void DecoratorArguments_AreOrdinaryExpressions_InSourceOrder() {
        CompilationUnit unit = ParseOk("@allowed(\"dev\", \"prod\")\nparam env: string\n");
        Decorator d = Assert.Single(Single<ParamDecl>(unit).Decorators);

        Assert.Equal(2, d.Arguments.Count);
        Assert.Equal("dev", TextOf(d.Arguments[0]));
        Assert.Equal("prod", TextOf(d.Arguments[1]));
    }

    /// <summary>
    /// D-424 Decision 1: a non-literal argument is not a parse error. The
    /// restriction to literals is the type checker's, so the expression reaches
    /// it intact and the diagnostic lands at the argument's own position.
    /// </summary>
    [Fact]
    public void NonLiteralArgument_ParsesAsAnExpression_NotAParseError() {
        CompilationUnit unit = ParseOk("@minLength(n)\nparam name: string\n");
        Decorator d = Assert.Single(Single<ParamDecl>(unit).Decorators);

        IdentifierExpr arg = Assert.IsType<IdentifierExpr>(Assert.Single(d.Arguments));
        Assert.Equal("n", arg.Name);
        Assert.Equal(1, arg.Range.Start.Line);
        Assert.Equal(12, arg.Range.Start.Column);
    }

    [Fact]
    public void EmptyArgumentList_ParsesToZeroArguments() {
        CompilationUnit unit = ParseOk("@allowed()\nparam env: string\n");
        Decorator d = Assert.Single(Single<ParamDecl>(unit).Decorators);
        Assert.Empty(d.Arguments);
    }

    /// <summary>
    /// D-421 made an optional trailing comma uniform across every
    /// comma-separated list; a decorator argument list is now one of them.
    /// </summary>
    [Fact]
    public void TrailingCommaInArgumentList_IsAccepted() {
        CompilationUnit unit = ParseOk("@allowed(\"dev\",)\nparam env: string\n");
        Decorator d = Assert.Single(Single<ParamDecl>(unit).Decorators);
        Assert.Equal("dev", TextOf(Assert.Single(d.Arguments)));
    }

    // -----------------------------------------------------------------------
    // ParamDecl.Range covers the stack (D-424 Decision 1)
    // -----------------------------------------------------------------------

    [Fact]
    public void ParamDeclRange_StartsAtTheDecoratorStack_NotTheParamKeyword() {
        CompilationUnit unit = ParseOk("@secure\nparam token: string\n");
        ParamDecl p = Single<ParamDecl>(unit);

        Assert.Equal(1, p.Range.Start.Line);
        Assert.Equal(1, p.Range.Start.Column);
    }

    [Fact]
    public void ParamDeclRange_StartsAtTheFirstDecoratorOfAStack() {
        CompilationUnit unit = ParseOk(
            "@minValue(0)\n@maxValue(100)\nparam threshold: int = 80\n");
        ParamDecl p = Single<ParamDecl>(unit);

        Assert.Equal(1, p.Range.Start.Line);
        Assert.Equal(1, p.Range.Start.Column);
    }

    [Fact]
    public void ParamDeclRange_Undecorated_StillStartsAtTheParamKeyword() {
        CompilationUnit unit = ParseOk("param token: string\n");
        ParamDecl p = Single<ParamDecl>(unit);

        Assert.Equal(1, p.Range.Start.Line);
        Assert.Equal(1, p.Range.Start.Column);
        Assert.Empty(p.Decorators);
    }

    // -----------------------------------------------------------------------
    // Decorators are a `param`-only construct (§19, D-424 Decision 2).
    //
    // Before D-424 `fn f(@secure a: int)` parsed clean, and a test asserted it
    // did, justified by the claim that "a function parameter list (§12) shares
    // the same decorator syntax and keeps it inline". §12 says nothing about
    // decorators; the claim entered the corpus through a scanner shared between
    // two productions, never through a decision. Its own fixture makes the
    // point — `@secure` on an `int` is invalid under D-411 twice over.
    // -----------------------------------------------------------------------

    [Fact]
    public void DecoratorInFunctionParameterList_IsE4002_AndTheSignatureStillParses() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("fn f(@secure a: int): int { return 1 }\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4002", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);

        // Recovery: the rest of the signature and the body are intact.
        FnDecl fn = Single<FnDecl>(unit);
        Parameter p = Assert.Single(fn.Parameters);
        Assert.Equal("a", p.Name);
        Assert.Equal("int", p.Type!.Name);
        Assert.Single(fn.Body.Statements);
    }

    [Fact]
    public void DecoratorWithArgumentsInFunctionParameterList_IsE4002() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("fn f(@minLength(3) a: string): int { return 1 }\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4002", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
        Assert.Equal("a", Assert.Single(Single<FnDecl>(unit).Parameters).Name);
    }

    /// <summary>
    /// Each misplaced decorator is its own mistake and gets its own diagnostic —
    /// on the same parameter or on different ones.
    /// </summary>
    [Fact]
    public void SeveralDecoratorsInAFunctionParameterList_EachReportsE4002() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("fn f(@secure a: string, @minLength(1) b: string): int { return 1 }\n");

        Assert.Equal(2, bag.Diagnostics.Count);
        Assert.All(bag.Diagnostics, d => Assert.Equal("E4002", d.Code));
        Assert.Equal(6, bag.Diagnostics[0].Range.Start.Column);
        Assert.Equal(25, bag.Diagnostics[1].Range.Start.Column);
        Assert.Equal(2, Single<FnDecl>(unit).Parameters.Count);
    }

    // -----------------------------------------------------------------------
    // Recovery and layer invariant (§29) — a malformed decorator is an ordinary
    // malformed construct, with no bespoke recovery path.
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedArgumentExpression_RecoversAndTheNextDeclarationStillParses() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("@allowed(1 +)\nparam env: string\nparam other: int\n");

        Assert.NotEmpty(bag.Diagnostics);
        ParamDecl other = Assert.IsType<ParamDecl>(unit.TopLevel[^1]);
        Assert.Equal("other", other.Name);
    }

    [Theory]
    [InlineData("@secure\nparam foo: string\n")]
    [InlineData("@allowed(\nparam foo: string\n")]
    [InlineData("@allowed(,)\nparam foo: string\n")]
    [InlineData("@allowed(1 2)\nparam foo: string\n")]
    [InlineData("@minLength(-1)\nparam foo: string\n")]
    [InlineData("@\nparam foo: string\n")]
    [InlineData("@@\nparam foo: string\n")]
    [InlineData("@secure(\n")]
    public void PathologicalDecoratorInput_NeverThrows(string src) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        if (bag.Diagnostics.Count > 0) return; // lexer-level rejection is fine
        Exception? ex = Record.Exception(() => Parser.Parse(tokens, bag));
        Assert.Null(ex);
    }
}
