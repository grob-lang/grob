using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 5 Increment E — flow-sensitive narrowing.
/// Inside an <c>if (x != nil) { }</c> block the checker narrows <c>x</c> from
/// <c>T?</c> to <c>T</c> for the block's extent, then removes the narrowing.
/// A dereference inside the narrowed block type-checks; the same dereference
/// outside the block remains <c>E0101</c>.
/// </summary>
public sealed class TypeCheckerNarrowingTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag);
        checker.Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    /// <summary>Collects every <see cref="IdentifierExpr"/> node in declaration order.</summary>
    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) {
            Identifiers.Add(node);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static List<IdentifierExpr> IdentifiersNamed(CompilationUnit unit, string name) {
        var collector = new IdentifierCollector();
        collector.VisitCompilationUnit(unit);
        return collector.Identifiers.FindAll(i => i.Name == name);
    }

    // -----------------------------------------------------------------------
    // Inside if (x != nil) the identifier carries the non-nullable type.
    // -----------------------------------------------------------------------

    [Fact]
    public void Narrowing_InsideNilGuard_IdentifierHasNonNullableType() {
        var (unit, diag) = TypeCheckSource("""
            x: string? := "hi"
            if (x != nil) {
                y := x
            }
            """);
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");

        // The reference to x inside the block (the RHS of `y := x`) must be String, not NullableString.
        // The reference to x in the guard `x != nil` is also present; both are non-nullable
        // once narrowed, but at minimum the in-block reference is narrowed.
        List<IdentifierExpr> xs = IdentifiersNamed(unit, "x");
        IdentifierExpr inBlock = xs[^1]; // last x reference is the RHS inside the block
        Assert.Equal(GrobType.String, inBlock.ResolvedType);
    }

    // -----------------------------------------------------------------------
    // After the block, x reverts to its nullable type.
    // -----------------------------------------------------------------------

    [Fact]
    public void Narrowing_AfterNilGuardBlock_IdentifierRevertsToNullable() {
        var (unit, diag) = TypeCheckSource("""
            x: string? := "hi"
            if (x != nil) {
                y := x
            }
            z := x
            """);
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");

        // The final reference (RHS of `z := x`, outside the block) must be NullableString again.
        List<IdentifierExpr> xs = IdentifiersNamed(unit, "x");
        IdentifierExpr afterBlock = xs[^1];
        Assert.Equal(GrobType.NullableString, afterBlock.ResolvedType);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant: every identifier node carries a non-null ResolvedType
    // and Declaration, narrowed or not.
    // -----------------------------------------------------------------------

    [Fact]
    public void Narrowing_AllIdentifierNodes_CarryResolvedTypeAndDeclaration() {
        var (unit, _) = TypeCheckSource("""
            x: string? := "hi"
            if (x != nil) {
                y := x
            }
            z := x
            """);
        foreach (IdentifierExpr id in IdentifiersNamed(unit, "x")) {
            Assert.NotEqual(GrobType.Unknown, id.ResolvedType);
            Assert.NotNull(id.Declaration);
        }
    }

    // -----------------------------------------------------------------------
    // A member access inside the narrowed block does not emit E0101.
    // -----------------------------------------------------------------------

    [Fact]
    public void Narrowing_MemberAccessInsideNilGuard_NoE0101() {
        var diag = Check("""
            x: string? := "hi"
            if (x != nil) {
                print(x.length)
            }
            """);
        Assert.DoesNotContain(diag.Errors, d => d.Code == "E0101");
    }

    // -----------------------------------------------------------------------
    // The same member access OUTSIDE the block still emits E0101.
    // -----------------------------------------------------------------------

    [Fact]
    public void Narrowing_MemberAccessOutsideNilGuard_EmitsE0101() {
        var diag = Check("""
            x: string? := "hi"
            if (x != nil) {
                print(x.length)
            }
            print(x.length)
            """);
        Diagnostic err = Assert.Single(diag.Errors);
        Assert.Equal("E0101", err.Code);
        Assert.Equal((5, 7), (err.Range.Start.Line, err.Range.Start.Column));
    }

    // -----------------------------------------------------------------------
    // The inverted guard form (nil != x) narrows too.
    // -----------------------------------------------------------------------

    [Fact]
    public void Narrowing_InvertedNilGuard_AlsoNarrows() {
        var diag = Check("""
            x: string? := "hi"
            if (nil != x) {
                print(x.length)
            }
            """);
        Assert.DoesNotContain(diag.Errors, d => d.Code == "E0101");
    }

    // -----------------------------------------------------------------------
    // Nested if (x != nil): the inner guard does not un-narrow the outer
    // binding on its exit.
    // -----------------------------------------------------------------------

    [Fact]
    public void Narrowing_NestedNilGuard_InnerDoesNotUnnarrowOuter() {
        var (unit, diag) = TypeCheckSource("""
            x: string? := "hi"
            if (x != nil) {
                if (x != nil) {
                    a := x
                }
                b := x
            }
            """);
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");

        // The reference to x in `b := x` (after the inner block, still inside the outer
        // block) must remain narrowed to String.
        List<IdentifierExpr> xs = IdentifiersNamed(unit, "x");
        IdentifierExpr bRhs = xs[^1];
        Assert.Equal(GrobType.String, bRhs.ResolvedType);
    }
}
