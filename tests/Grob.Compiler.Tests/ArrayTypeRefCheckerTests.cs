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
    public void ArrayAnnotation_RejectsIncompatibleInitializer_ProvesMeaningfulResolution() {
        // If int[] fell back to GrobType.Unknown the permissive path would accept this.
        // E0001 here proves the annotation resolved to GrobType.Array, not Unknown.
        DiagnosticBag bag = Check("items: int[] := 5\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(17, d.Range.Start.Column);
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
        Diagnostic dRejected = Assert.Single(nonNullableArray.Errors);
        Assert.Equal("E0001", dRejected.Code);
        Assert.Equal(1, dRejected.Range.Start.Line);
        Assert.Equal(18, dRejected.Range.Start.Column);
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
