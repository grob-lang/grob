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

    // TODO(Sprint 2): after the type checker lands, add an invariant test that
    // runs a full type-check pass and asserts every IdentifierExpr in the AST
    // has non-null ResolvedType and non-null Declaration.
}
