using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Verifies the day-one LSP metadata shape on <see cref="IdentifierExpr"/>
/// required by D-137 and grob-tooling-strategy.md §"Foundational Constraint —
/// Source Location From Day One".
/// </summary>
public class IdentifierLspShapeTests {
    /// <summary>
    /// <see cref="IdentifierExpr.ResolvedType"/> must exist as a settable property
    /// and default to <see cref="GrobType.Unknown"/> after Sprint 1 parsing.
    /// </summary>
    [Fact]
    public void ResolvedType_ExistsAndDefaultsToUnknown() {
        Expression e = ExprOf(ParseOk("foo\n"));
        IdentifierExpr ident = Assert.IsType<IdentifierExpr>(e);

        Assert.Equal(GrobType.Unknown, ident.ResolvedType);
    }

    /// <summary>
    /// <see cref="IdentifierExpr.ResolvedType"/> must be settable so the type
    /// checker (Sprint 2) can populate it without retrofitting the class.
    /// </summary>
    [Fact]
    public void ResolvedType_IsMutable() {
        Expression e = ExprOf(ParseOk("bar\n"));
        IdentifierExpr ident = Assert.IsType<IdentifierExpr>(e);

        ident.ResolvedType = GrobType.Unknown; // round-trip: set then read back
        Assert.Equal(GrobType.Unknown, ident.ResolvedType);
    }

    /// <summary>
    /// <see cref="IdentifierExpr.Declaration"/> must exist as a settable nullable
    /// property and be <see langword="null"/> after Sprint 1 parsing.
    /// </summary>
    [Fact]
    public void Declaration_ExistsAndDefaultsToNull() {
        Expression e = ExprOf(ParseOk("baz\n"));
        IdentifierExpr ident = Assert.IsType<IdentifierExpr>(e);

        Assert.Null(ident.Declaration);
    }

    /// <summary>
    /// <see cref="IdentifierExpr.Declaration"/> must be settable so the type
    /// checker (Sprint 2) can populate it without retrofitting the class.
    /// </summary>
    [Fact]
    public void Declaration_IsMutable() {
        Expression e = ExprOf(ParseOk("qux\n"));
        IdentifierExpr ident = Assert.IsType<IdentifierExpr>(e);

        // Use the node itself as a stand-in target — any non-null AstNode will do.
        ident.Declaration = ident;
        Assert.Same(ident, ident.Declaration);

        ident.Declaration = null;
        Assert.Null(ident.Declaration);
    }

    // Sprint 2: once the type checker is present, add a test here that runs a
    // full type-check pass over a snippet and asserts every IdentifierExpr in
    // the resulting AST has a non-null ResolvedType and a non-null Declaration.
    // Example skeleton (do not unskip until the type checker exists):
    //
    // [Fact(Skip = "Sprint 2: requires type checker")]
    // public void AfterTypeCheck_AllIdentifiersHaveResolvedTypeAndDeclaration() { ... }
}
