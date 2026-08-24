using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for D-415's <see cref="ParamDecl"/>. Parameter <i>binding</i>
/// is Sprint 10 (D-412), so <c>VisitParamDecl</c> registers no symbol and the
/// declaration itself contributes no type. Its default expression is a different
/// matter: it is an ordinary expression sitting in the tree, and the §3.1.1
/// invariant admits no exemption for it — every identifier node carries a non-null
/// <c>ResolvedType</c> and a non-null <c>Declaration</c> after type-check, with
/// <see cref="GrobType.Error"/> and <see cref="UnresolvedDecl.Instance"/> as the
/// error-path sentinels (D-311). <see cref="AstWalker.VisitParamDecl"/> already
/// walks into the default; the checker must agree.
/// </summary>
public sealed class TypeCheckerParamDeclTests {
    private static DiagnosticBag Check(string source, out CompilationUnit unit) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        Assert.Empty(bag.Diagnostics);
        unit = Parser.Parse(tokens, bag);
        Assert.Empty(bag.Diagnostics);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    [Fact]
    public void ParamDecl_LiteralDefault_TypeChecksWithNoDiagnostics() {
        DiagnosticBag bag = Check("param limit: int = 10\n", out CompilationUnit unit);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        ParamDecl p = Assert.IsType<ParamDecl>(unit.TopLevel[0]);
        Assert.NotNull(p.DefaultValue);
    }

    [Fact]
    public void ParamDecl_NoDefault_TypeChecksWithNoDiagnostics() {
        DiagnosticBag bag = Check("param limit: int\n", out CompilationUnit unit);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        ParamDecl p = Assert.IsType<ParamDecl>(unit.TopLevel[0]);
        Assert.Null(p.DefaultValue);
    }

    /// <summary>
    /// The gap this test closes: an unresolved identifier in a parameter default
    /// went unreported, and the node was left with an unset <c>ResolvedType</c>
    /// and a null <c>Declaration</c> — a §3.1.1 violation, not merely a missing
    /// diagnostic.
    /// </summary>
    [Fact]
    public void ParamDecl_UnresolvedIdentifierDefault_ReportsE1001AndSetsErrorSentinels() {
        DiagnosticBag bag = Check("param limit: int = fallback\n", out CompilationUnit unit);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1001.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(20, diag.Range.Start.Column);

        ParamDecl p = Assert.IsType<ParamDecl>(unit.TopLevel[0]);
        IdentifierExpr id = Assert.IsType<IdentifierExpr>(p.DefaultValue);
        // §3.1.1 sentinels. GrobType carries value equality, so the type side is
        // compared by value; the declaration sentinel is a singleton and is
        // asserted by reference (D-311).
        Assert.Equal(GrobType.Error, id.ResolvedType);
        Assert.Same(UnresolvedDecl.Instance, id.Declaration);
    }

    /// <summary>
    /// The resolving counterpart: a default referring to a declared
    /// <c>const</c> resolves to that declaration, so the invariant holds on the
    /// success path too and not only through the error sentinels.
    /// </summary>
    [Fact]
    public void ParamDecl_ConstIdentifierDefault_ResolvesToItsDeclaration() {
        DiagnosticBag bag = Check("const fallback := 10\nparam limit: int = fallback\n",
            out CompilationUnit unit);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");

        ParamDecl p = Assert.IsType<ParamDecl>(unit.TopLevel[^1]);
        IdentifierExpr id = Assert.IsType<IdentifierExpr>(p.DefaultValue);
        Assert.NotNull(id.ResolvedType);
        Assert.NotEqual(GrobType.Error, id.ResolvedType);
        Assert.NotSame(UnresolvedDecl.Instance, id.Declaration);
    }

    /// <summary>
    /// A decorated declaration reaches the same default-expression check — the
    /// decorator stack is part of the production (§19), not a separate node, so
    /// it must not shadow the traversal.
    /// </summary>
    [Fact]
    public void ParamDecl_DecoratedWithUnresolvedDefault_StillReportsE1001() {
        DiagnosticBag bag = Check("@secure\nparam token: string = fallback\n", out _);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1001.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(23, diag.Range.Start.Column);
    }
}
