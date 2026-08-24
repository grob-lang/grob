using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser tests for the braceless <c>param</c> declaration grammar (D-410):
/// <c>{ decorator newline } "param" identifier ":" type [ "=" expression ] newline</c>.
/// One node per <c>param</c> keyword (<see cref="ParamDecl"/>) — the block form
/// (<c>param { ... }</c>) is retired and no longer parses. See
/// <see cref="ParserParamDeclRecoveryTests"/> for recovery-mechanics coverage
/// and <c>ParserDeclarationTests.ParamBlock_NoLongerParses</c> for the
/// retirement diagnostic.
/// </summary>
public sealed class ParserParamDeclTests {
    // -----------------------------------------------------------------------
    // Canonical form
    // -----------------------------------------------------------------------

    [Fact]
    public void Bare_NoDefault_ParsesToSingleDeclaration() {
        CompilationUnit unit = ParseOk("param token: string\n");
        ParamDecl p = Single<ParamDecl>(unit);
        Assert.Equal("token", p.Name);
        Assert.Equal("string", p.Type.Name);
        Assert.Null(p.DefaultValue);

        // D-137: every parameter node carries a source location, populated
        // from the real parse position — not a default/zero sentinel.
        Assert.Equal(1, p.Range.Start.Line);
        Assert.Equal(1, p.Range.Start.Column);
    }

    [Fact]
    public void WithDefault_ParsesDefaultExpression() {
        CompilationUnit unit = ParseOk("param threshold: int = 80\n");
        ParamDecl p = Single<ParamDecl>(unit);
        Assert.Equal("threshold", p.Name);
        Assert.Equal("int", p.Type.Name);
        Assert.Equal(80L, Assert.IsType<IntLiteralExpr>(p.DefaultValue).Value);
    }

    [Fact]
    public void SingleDecorator_IsSkipped_DeclarationParsesCleanly() {
        CompilationUnit unit = ParseOk("@secure\nparam token: string\n");
        ParamDecl p = Single<ParamDecl>(unit);
        Assert.Equal("token", p.Name);
        // The declaration's own range starts at 'param' (line 2), not the
        // decorator — decorators are parsed and skipped, not yet captured
        // into the AST (Sprint 10).
        Assert.Equal(2, p.Range.Start.Line);
        Assert.Equal(1, p.Range.Start.Column);
    }

    [Fact]
    public void DecoratorStack_MultipleDecorators_DeclarationParsesCleanly() {
        CompilationUnit unit = ParseOk(
            "@minValue(0)\n@maxValue(100)\nparam threshold: int = 80\n");
        ParamDecl p = Single<ParamDecl>(unit);
        Assert.Equal("threshold", p.Name);
        Assert.Equal("int", p.Type.Name);
        Assert.Equal(80L, Assert.IsType<IntLiteralExpr>(p.DefaultValue).Value);
        Assert.Equal(3, p.Range.Start.Line);
    }

    /// <summary>
    /// §3.10's canonical shape: a decorated declaration isolated by a blank
    /// line, followed by a run of undecorated declarations with no blank
    /// lines between them. All three are one parameter group by contiguity
    /// (§19) even though they span two formatting groups — no dedicated
    /// grouping code exists; each is just its own top-level item.
    /// </summary>
    [Fact]
    public void MixedGroup_DecoratedAndUndecorated_AllParseIndependently() {
        CompilationUnit unit = ParseOk(
            "@secure\n" +
            "param token: string\n" +
            "\n" +
            "param source_dir: string\n" +
            "param dest_dir: string\n");

        Assert.Equal(3, unit.TopLevel.Count);
        ParamDecl token = Assert.IsType<ParamDecl>(unit.TopLevel[0]);
        ParamDecl sourceDir = Assert.IsType<ParamDecl>(unit.TopLevel[1]);
        ParamDecl destDir = Assert.IsType<ParamDecl>(unit.TopLevel[2]);
        Assert.Equal("token", token.Name);
        Assert.Equal("source_dir", sourceDir.Name);
        Assert.Equal("dest_dir", destDir.Name);
    }

    // -----------------------------------------------------------------------
    // The block form is retired (D-410)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("param {\ntoken: string\n}\n", 1, 7)]
    [InlineData("param{}\n", 1, 6)]
    public void BlockForm_NoLongerParses_ProducesE4201(string src, int line, int column) {
        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", d.Code);
        Assert.Equal("expected parameter name after 'param'", d.Message);
        Assert.Equal(line, d.Range.Start.Line);
        Assert.Equal(column, d.Range.Start.Column);
        Assert.NotNull(unit);
    }

    // -----------------------------------------------------------------------
    // E4201 — malformed param declaration syntax (D-410's first throw site)
    // -----------------------------------------------------------------------

    [Fact]
    public void MissingTypeAnnotation_IsE4201_NotE2001() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("param foo\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", d.Code);
        Assert.Equal("expected ':' after parameter name — the type annotation is mandatory", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
        Assert.NotNull(unit);
    }

    [Fact]
    public void ColonAssignDefault_IsE4201_NotE2001() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("param foo: string := \"x\"\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", d.Code);
        Assert.Equal("expected '=' for a parameter default, not ':='", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(19, d.Range.Start.Column);
        Assert.NotNull(unit);
    }

    [Fact]
    public void DecoratorNotFollowedByParam_IsE4201() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("@secure\nfn f(): int { return 1 }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", d.Code);
        Assert.Equal("expected 'param' after decorator", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
        // The following fn is recovered independently, not lost.
        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("f", fn.Name);
    }

    /// <summary>
    /// §19's production is <c>{ decorator newline } "param" …</c> — the newline
    /// after each decorator is part of the grammar, not layout, and the prose
    /// rule ("decorators sit on their own line immediately above the `param`
    /// they modify") says the same. The inline form is therefore rejected.
    /// </summary>
    [Fact]
    public void DecoratorOnSameLineAsParam_IsE4201() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("@secure param token: string\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", d.Code);
        Assert.Equal("expected a newline after the decorator — a decorator sits on its own line above 'param'",
            d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
        Assert.NotNull(unit);
    }

    /// <summary>
    /// The companion to the above: the newline requirement belongs to the
    /// top-level production alone. A function parameter list (§12) shares the
    /// same decorator syntax and keeps it inline, so the rejection must not
    /// leak through the scanner both productions call.
    /// </summary>
    [Fact]
    public void DecoratorInlineInFunctionParameterList_StillParses() {
        CompilationUnit unit = ParseOk("fn f(@secure a: int): int { return 1 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        Assert.Equal("a", Assert.Single(fn.Parameters).Name);
    }

    // -----------------------------------------------------------------------
    // Layer invariant (tdd-cycle step 4): malformed but parseable param input
    // never crashes the parser — always a node or a diagnostic.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("param\n")]
    [InlineData("param:\n")]
    [InlineData("param foo:\n")]
    [InlineData("@\nparam foo: string\n")]
    [InlineData("@secure(\nparam foo: string\n")]
    [InlineData("param foo: string = \n")]
    public void PathologicalParamInput_NeverThrows(string src) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        if (bag.Diagnostics.Count > 0) return; // lexer-level rejection is fine
        Exception? ex = Record.Exception(() => Parser.Parse(tokens, bag));
        Assert.Null(ex);
    }
}
