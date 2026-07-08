using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker (and parser, for E2206) tests for Sprint 7 Increment C —
/// <c>finally</c>: E2206 (<c>finally</c> not last in <c>try</c>, raised by the
/// parser since <see cref="Ast.TryStmt"/>'s shape cannot represent the
/// violation), E2207 (<c>return</c>/<c>break</c>/<c>continue</c> inside
/// <c>finally</c>), and the D-276 carve-out (control flow inside a nested
/// block-body lambda is unaffected). No emission (Increment C compiler/VM
/// tests live elsewhere).
/// </summary>
public sealed class TypeCheckerFinallyTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        Ast.CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    // -----------------------------------------------------------------------
    // E2206 — finally not last in try (D-275; raised by the parser, D-300
    // recovery — the whole try statement becomes an ErrorStmt).
    // -----------------------------------------------------------------------

    [Fact]
    public void Finally_FollowedByCatch_EmitsE2206() {
        DiagnosticBag bag = Check("""
            try { x := 1 } finally { y := 2 } catch (e: IoError) { z := 3 }
            """);

        Assert.Contains(bag.Errors, d => d.Code == "E2206");
    }

    [Fact]
    public void Finally_FollowedByAnotherFinally_EmitsE2206() {
        DiagnosticBag bag = Check("""
            try { x := 1 } finally { y := 2 } finally { z := 3 }
            """);

        Assert.Contains(bag.Errors, d => d.Code == "E2206");
    }

    [Fact]
    public void Finally_Last_NoE2206() {
        DiagnosticBag bag = Check("""
            try { x := 1 } catch (e: IoError) { y := 2 } finally { z := 3 }
            """);

        Assert.DoesNotContain(bag.Errors, d => d.Code == "E2206");
    }

    [Fact]
    public void TryFinallyOnly_NoCatch_TypeChecksClean() {
        DiagnosticBag bag = Check("try { x := 1 } finally { y := 2 }\n");

        Assert.False(bag.HasErrors, ParserTestHelpers.FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // E2207 — return / break / continue directly inside finally (D-275).
    // -----------------------------------------------------------------------

    [Fact]
    public void Finally_ReturnDirectlyInside_EmitsE2207() {
        DiagnosticBag bag = Check("""
            fn f(): int {
                try { x := 1 } finally { return 1 }
                return 2
            }
            """);

        Assert.Contains(bag.Errors, d => d.Code == "E2207");
    }

    [Fact]
    public void Finally_BreakDirectlyInside_EmitsE2207() {
        DiagnosticBag bag = Check("""
            while (true) {
                try { x := 1 } finally { break }
            }
            """);

        Assert.Contains(bag.Errors, d => d.Code == "E2207");
    }

    [Fact]
    public void Finally_ContinueDirectlyInside_EmitsE2207() {
        DiagnosticBag bag = Check("""
            while (true) {
                try { x := 1 } finally { continue }
            }
            """);

        Assert.Contains(bag.Errors, d => d.Code == "E2207");
    }

    // -----------------------------------------------------------------------
    // Not E2207 — a loop nested inside the finally shields its own break/continue.
    // -----------------------------------------------------------------------

    [Fact]
    public void Finally_BreakInLoopNestedInside_IsLegal() {
        DiagnosticBag bag = Check("""
            try { x := 1 } finally { while (true) { break } }
            """);

        Assert.DoesNotContain(bag.Errors, d => d.Code == "E2207");
    }

    [Fact]
    public void Finally_ContinueInLoopNestedInside_IsLegal() {
        DiagnosticBag bag = Check("""
            try { x := 1 } finally { while (true) { continue } }
            """);

        Assert.DoesNotContain(bag.Errors, d => d.Code == "E2207");
    }

    // -----------------------------------------------------------------------
    // D-276 carve-out — return inside a block-body lambda nested inside
    // finally exits only the lambda; not E2207.
    // -----------------------------------------------------------------------

    [Fact]
    public void Finally_ReturnInsideNestedBlockLambda_IsLegal() {
        DiagnosticBag bag = Check("""
            try { x := 1 } finally { cb := () => { return 5 } }
            """);

        Assert.DoesNotContain(bag.Errors, d => d.Code == "E2207");
    }

    [Fact]
    public void Finally_ReturnInsideNestedBlockLambdaInsideFunction_IsLegal() {
        DiagnosticBag bag = Check("""
            fn f(): int {
                try { x := 1 } finally { cb := () => { return 5 } }
                return 2
            }
            """);

        Assert.DoesNotContain(bag.Errors, d => d.Code == "E2207");
    }

    // -----------------------------------------------------------------------
    // Layer invariant — pathological but parseable finally shapes never throw.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("try { x := 1 } finally { y := 2 } catch (e: IoError) { z := 3 }\n")]
    [InlineData("try { x := 1 } finally { y := 2 } finally { z := 3 }\n")]
    [InlineData("fn f(): int {\ntry { x := 1 } finally { return 1 }\nreturn 2\n}\n")]
    [InlineData("while (true) {\ntry { x := 1 } finally { break }\n}\n")]
    [InlineData("try { x := 1 } finally { while (true) { break } }\n")]
    [InlineData("try { x := 1 } finally { cb := () => { return 5 } }\n")]
    public void Finally_PathologicalShapes_NeverThrows(string source) {
        Exception? thrown = Record.Exception(() => Check(source));
        Assert.Null(thrown);
    }
}
