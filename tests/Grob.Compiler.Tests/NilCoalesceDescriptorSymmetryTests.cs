using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-402 — restores <c>ArrayDescriptorOf</c>'s symmetry with <c>MapDescriptorOf</c>'s D-401
/// <c>BinaryExpr { Operator: NilCoalesce }</c> arm, which D-401 added for maps only (its own
/// acceptance test could not pass otherwise) and deliberately left the array side unfixed —
/// a citable finding, not an oversight. Both the array and map cases are asserted together
/// here, in one file, so the restored symmetry is visible in a single place rather than
/// scattered across the two collections' own dedicated test files.
/// </summary>
public sealed class NilCoalesceDescriptorSymmetryTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    // -----------------------------------------------------------------------
    // The array side — previously Unknown through '??', now resolves the element type.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayResult_ThroughNilCoalesce_IndexResolvesToElementType() {
        // Before this fix: 'first' typed Unknown (ArrayDescriptorOf had no BinaryExpr arm),
        // so this assignment failed E0001 ("Cannot assign value of type 'unknown'").
        DiagnosticBag bag = Check("""
            xs: int[]? := [1, 2, 3]
            first := (xs ?? [])[0]
            y: int := first
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // The map side — D-401's own fix, kept green and restated here so the symmetry with
    // the array case above is visible in one place rather than only in MapTypeDescriptorTests.
    // -----------------------------------------------------------------------

    [Fact]
    public void MapResult_ThroughNilCoalesce_IndexResolvesToValueType() {
        DiagnosticBag bag = Check("""
            fn f(m: map<string, int>?): void {
            n: int? := (m ?? map<string, int>{})["k"]
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Nested '??' — the descriptor arm recurses on both operands, so a chain of two or
    // more nil-coalesced operands resolves just as well as a single one.
    // -----------------------------------------------------------------------

    [Fact]
    public void NestedNilCoalesce_ArrayIndexResolvesElementType() {
        DiagnosticBag bag = Check("""
            a: int[]? := nil
            b: int[]? := nil
            c: int[] := [1, 2, 3]
            y: int := (a ?? b ?? c)[0]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void NestedNilCoalesce_MapIndexResolvesValueType() {
        DiagnosticBag bag = Check("""
            fn f(a: map<string, int>?, b: map<string, int>?): void {
            c: map<string, int> := map<string, int>{}
            y: int? := (a ?? b ?? c)["k"]
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }
}
