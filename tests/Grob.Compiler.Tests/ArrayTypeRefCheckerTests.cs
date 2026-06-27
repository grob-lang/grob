using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>TypeChecker tests for array type-references (D-327).</summary>
public sealed class ArrayTypeRefCheckerTests {
    // ------------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------------

    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag);
        checker.Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    // ------------------------------------------------------------------
    // Annotation → GrobType resolution
    // ------------------------------------------------------------------

    [Fact]
    public void ArrayAnnotation_ResolvesToArray() {
        // items: int[] := [] — annotation resolves to GrobType.Array, [] literal is accepted
        DiagnosticBag bag = Check("items: int[] := []\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void ArrayAnnotation_String_ResolvesToArray() {
        DiagnosticBag bag = Check("fn g(): string[] { return [] }\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void NullableArrayAnnotation_AcceptsNil() {
        // items: int[]? := nil — NullableArray accepts nil
        DiagnosticBag bag = Check("items: int[]? := nil\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void NullableArray_AcceptsNil_ProvesBehaviourallyDistinctFromArray() {
        // int[]? := nil works (array itself is nullable — NullableArray accepts nil)
        DiagnosticBag nullable = Check("items: int[]? := nil\n");
        Assert.False(nullable.HasErrors, FormatDiagnostics(nullable));
        // int?[] := nil fails (array is non-nullable — Array does not accept nil)
        DiagnosticBag nonNullableArray = Check("items: int?[] := nil\n");
        Assert.True(nonNullableArray.HasErrors, "int?[] should not accept nil");
    }

    [Fact]
    public void NullableArray_Widening_NonNullableArrayIsAssignable() {
        // A non-nullable array value is assignable to a nullable-array annotation
        DiagnosticBag bag = Check("""
            source: int[] := []
            dest: int[]? := source
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void ArrayAnnotation_Parameter_ForInIterates() {
        // fn f(xs: int[]): int — array param used in for...in is valid (array iteration shape)
        DiagnosticBag bag = Check("""
            fn f(xs: int[]): int {
                sum := 0
                for x in xs {
                    sum = sum + x
                }
                return sum
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void ArrayAnnotation_StructField_TypeChecks() {
        // type T { tags: string[] }
        DiagnosticBag bag = Check("type T { tags: string[] }\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Nesting (D-182)
    // ------------------------------------------------------------------

    [Fact]
    public void NestedArrayAnnotation_ResolvesToArray() {
        // int[][] and int[][][] both resolve to GrobType.Array (element type deferred to generics sprint)
        DiagnosticBag bag = Check("fn f(xs: int[][]): int { return 0 }\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        DiagnosticBag bag2 = Check("fn g(xs: int[][][]): int { return 0 }\n");
        Assert.False(bag2.HasErrors, FormatDiagnostics(bag2));
    }

    // ------------------------------------------------------------------
    // D-326 × D-327 interaction — makeCounter-adjacent
    // ------------------------------------------------------------------

    [Fact]
    public void FunctionReturningArrayAnnotation_TypeChecks() {
        // fn counter(): fn(): int[] — function returning int[]
        DiagnosticBag bag = Check("""
            fn counter(): fn(): int[] {
                return () => []
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void ArrayOfFunctionsAnnotation_TypeChecks() {
        // fn arrOfCounters(): (fn(): int)[] — array of functions
        DiagnosticBag bag = Check("""
            fn arrOfCounters(): (fn(): int)[] {
                return []
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }
}
