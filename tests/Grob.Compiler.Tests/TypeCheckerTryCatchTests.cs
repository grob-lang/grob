using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 7 Increment B — <c>try</c>/<c>catch</c> compile
/// errors (E2204, E2205, E0015, E2213), the polymorphic-catch permissiveness rule,
/// and the immutable catch binding. No <c>finally</c> (Increment C).
/// </summary>
public sealed class TypeCheckerTryCatchTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    // -----------------------------------------------------------------------
    // E2204 — try without catch or finally.
    // -----------------------------------------------------------------------

    [Fact]
    public void Try_WithoutCatchOrFinally_EmitsE2204() {
        DiagnosticBag bag = Check("try { x := 1 }\n");

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E2204", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
    }

    [Fact]
    public void Try_WithOnlyFinally_NoE2204() {
        // A try with only a finally (no catches) is legal (D-275) — not this
        // increment's concern beyond not falsely flagging it here.
        DiagnosticBag bag = Check("try { x := 1 } finally { y := 2 }\n");

        Assert.DoesNotContain(bag.Errors, d => d.Code == "E2204");
    }

    // -----------------------------------------------------------------------
    // E2205 — catch after catch-all (D-083).
    // -----------------------------------------------------------------------

    [Fact]
    public void Catch_AfterCatchAll_EmitsE2205() {
        DiagnosticBag bag = Check("""
            try { x := 1 } catch e { y := 2 } catch (f: IoError) { z := 3 }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E2205", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(35, d.Range.Start.Column);
    }

    [Fact]
    public void SecondCatchAll_AfterCatchAll_EmitsE2205() {
        DiagnosticBag bag = Check("""
            try { x := 1 } catch e { y := 2 } catch f { z := 3 }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E2205", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(35, d.Range.Start.Column);
    }

    [Fact]
    public void CatchAll_Last_NoError() {
        DiagnosticBag bag = Check("""
            try { x := 1 } catch (e: IoError) { y := 2 } catch f { z := 3 }
            """);

        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // E0015 — catch type is not a GrobError subtype.
    // -----------------------------------------------------------------------

    [Fact]
    public void Catch_NonGrobErrorType_EmitsE0015() {
        DiagnosticBag bag = Check("try { x := 1 } catch (e: int) { y := 2 }\n");

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0015", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(26, d.Range.Start.Column);
    }

    [Fact]
    public void Catch_GrobErrorRoot_IsLegalCatchAllEquivalent() {
        DiagnosticBag bag = Check("try { x := 1 } catch (e: GrobError) { y := 2 }\n");

        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    [Fact]
    public void Catch_LeafType_NoError() {
        DiagnosticBag bag = Check("try { x := 1 } catch (e: IoError) { y := 2 }\n");

        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // E2213 — duplicate catch for the same type.
    // -----------------------------------------------------------------------

    [Fact]
    public void Catch_DuplicateType_EmitsE2213() {
        DiagnosticBag bag = Check("""
            try { x := 1 } catch (e: IoError) { y := 2 } catch (f: IoError) { z := 3 }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E2213", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(56, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Permissiveness (§27) — no can-throw analysis; a catch for a type the try
    // body cannot actually throw still type-checks cleanly.
    // -----------------------------------------------------------------------

    [Fact]
    public void Catch_ForUnthrowableType_NoError() {
        DiagnosticBag bag = Check("try { x := 1 } catch (e: IoError) { y := 2 }\n");

        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // The immutable catch binding — reassignment reuses E0202 (readonly).
    // -----------------------------------------------------------------------

    [Fact]
    public void CatchBinding_Reassigned_EmitsE0202() {
        DiagnosticBag bag = Check("""
            try { x := 1 } catch (e: IoError) { e = IoError { message: "y" } }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0202", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(37, d.Range.Start.Column);
    }

    [Fact]
    public void CatchAllBinding_Reassigned_EmitsE0202() {
        DiagnosticBag bag = Check("""
            try { x := 1 } catch e { e = GrobError { message: "y" } }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0202", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(26, d.Range.Start.Column);
    }

    [Fact]
    public void CatchBinding_FieldAccess_NoError() {
        // The binding resolves member access through the declared type (IoError),
        // same mechanism as any other struct-typed local.
        DiagnosticBag bag = Check("""
            try { x := 1 } catch (e: IoError) { m := e.message }
            """);

        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant — every identifier node has ResolvedType + Declaration.
    // -----------------------------------------------------------------------

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) { Identifiers.Add(node); return default; }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    [Fact]
    public void TryCatch_IdentifierNodes_HaveNonNullResolvedTypeAndDeclaration() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            try { x := 1 } catch (e: IoError) { m := e.message }
            """);

        IdentifierCollector collector = new();
        collector.Visit(unit);

        foreach (IdentifierExpr id in collector.Identifiers) {
            Assert.True(id.ResolvedType != GrobType.Unknown || bag.HasErrors,
                $"Identifier '{id.Name}' at {id.Range} has UnknownType after type-check.");
            Assert.NotSame(UnresolvedDecl.Instance, id.Declaration);
            Assert.NotNull(id.Declaration);
        }
    }

    // -----------------------------------------------------------------------
    // Layer invariant — pathological but parseable try/catch shapes never throw.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("try { x := 1 } catch (e: int) { y := 2 }\n")]
    [InlineData("try { x := 1 } catch e { y := 2 } catch f { z := 3 }\n")]
    [InlineData("try { x := 1 } catch (e: IoError) { y := 2 } catch (f: IoError) { z := 3 }\n")]
    [InlineData("try { x := 1 } catch e { y := 2 } catch (f: IoError) { z := 3 }\n")]
    [InlineData("try { x := 1 }\n")]
    public void TryCatch_PathologicalShapes_NeverThrows(string source) {
        Exception? thrown = Record.Exception(() => Check(source));
        Assert.Null(thrown);
    }
}
