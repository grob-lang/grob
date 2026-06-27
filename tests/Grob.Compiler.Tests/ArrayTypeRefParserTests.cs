using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>Parser tests for array type-references (D-327).</summary>
public sealed class ArrayTypeRefParserTests {
    // ------------------------------------------------------------------
    // Position coverage — int[] in every annotation position
    // ------------------------------------------------------------------

    [Fact]
    public void ArrayTypeRef_ParameterPosition_Parses() {
        CompilationUnit unit = ParseOk("fn f(xs: int[]): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        ArrayTypeRef arr = Assert.IsType<ArrayTypeRef>(fn.Parameters[0].Type);
        Assert.Equal("int", arr.ElementType.Name);
        Assert.False(arr.IsNullable);
        Assert.False(arr.ElementType.IsNullable);
    }

    [Fact]
    public void ArrayTypeRef_ReturnPosition_Parses() {
        CompilationUnit unit = ParseOk("fn g(): string[] { return [] }\n");
        FnDecl fn = Single<FnDecl>(unit);
        ArrayTypeRef arr = Assert.IsType<ArrayTypeRef>(fn.ReturnType);
        Assert.Equal("string", arr.ElementType.Name);
        Assert.False(arr.IsNullable);
    }

    [Fact]
    public void ArrayTypeRef_BindingAnnotation_Parses() {
        CompilationUnit unit = ParseOk("items: int[] := []\n");
        VarDeclStmt v = Single<VarDeclStmt>(unit);
        ArrayTypeRef arr = Assert.IsType<ArrayTypeRef>(v.AnnotatedType);
        Assert.Equal("int", arr.ElementType.Name);
        Assert.False(arr.IsNullable);
    }

    [Fact]
    public void ArrayTypeRef_StructFieldPosition_Parses() {
        CompilationUnit unit = ParseOk("type T { tags: string[] }\n");
        TypeDecl td = Single<TypeDecl>(unit);
        ArrayTypeRef arr = Assert.IsType<ArrayTypeRef>(td.Fields[0].Type);
        Assert.Equal("string", arr.ElementType.Name);
        Assert.False(arr.IsNullable);
    }

    // ------------------------------------------------------------------
    // Suffix precedence — int[]? vs int?[]
    // ------------------------------------------------------------------

    [Fact]
    public void ArrayTypeRef_NullableArray_SuffixOnArray() {
        // int[]? — nullable array of int: ? applies to the array, not the element
        CompilationUnit unit = ParseOk("fn f(xs: int[]?): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        ArrayTypeRef arr = Assert.IsType<ArrayTypeRef>(fn.Parameters[0].Type);
        Assert.True(arr.IsNullable);
        Assert.Equal("int", arr.ElementType.Name);
        Assert.False(arr.ElementType.IsNullable);
    }

    [Fact]
    public void ArrayTypeRef_ArrayOfNullable_SuffixOnElement() {
        // int?[] — array of nullable int: ? applies to the element, not the array
        CompilationUnit unit = ParseOk("fn f(xs: int?[]): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        ArrayTypeRef arr = Assert.IsType<ArrayTypeRef>(fn.Parameters[0].Type);
        Assert.False(arr.IsNullable);
        Assert.Equal("int", arr.ElementType.Name);
        Assert.True(arr.ElementType.IsNullable);
    }

    [Fact]
    public void ArrayTypeRef_NullableArray_DifferentFrom_ArrayOfNullable() {
        // int[]? and int?[] parse to structurally distinct trees.
        CompilationUnit unitA = ParseOk("fn f(xs: int[]?): int { return 0 }\n");
        CompilationUnit unitB = ParseOk("fn g(xs: int?[]): int { return 0 }\n");
        ArrayTypeRef arrA = Assert.IsType<ArrayTypeRef>(Single<FnDecl>(unitA).Parameters[0].Type);
        ArrayTypeRef arrB = Assert.IsType<ArrayTypeRef>(Single<FnDecl>(unitB).Parameters[0].Type);
        // arrA is nullable-array-of-int; arrB is array-of-nullable-int — structurally distinct
        Assert.True(arrA.IsNullable);
        Assert.False(arrA.ElementType.IsNullable);
        Assert.False(arrB.IsNullable);
        Assert.True(arrB.ElementType.IsNullable);
    }

    // ------------------------------------------------------------------
    // Suffix precedence — function-type interactions (D-326 × D-327)
    // ------------------------------------------------------------------

    [Fact]
    public void FunctionReturnsArray_SuffixBindsToReturn() {
        // fn(): int[] — the [] binds to the return type, not to the function itself
        CompilationUnit unit = ParseOk("fn f(): fn(): int[] { return () => [] }\n");
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef fnType = Assert.IsType<FunctionTypeRef>(fn.ReturnType);
        Assert.False(fnType.IsNullable);
        ArrayTypeRef returnArr = Assert.IsType<ArrayTypeRef>(fnType.ReturnType);
        Assert.Equal("int", returnArr.ElementType.Name);
        Assert.False(returnArr.IsNullable);
    }

    [Fact]
    public void ArrayOfFunctions_RequiresGroupingParens() {
        // (fn(): int)[] — parens make the function the element type of the array
        CompilationUnit unit = ParseOk("fn f(xs: (fn(): int)[]): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        ArrayTypeRef arr = Assert.IsType<ArrayTypeRef>(fn.Parameters[0].Type);
        Assert.False(arr.IsNullable);
        FunctionTypeRef innerFn = Assert.IsType<FunctionTypeRef>(arr.ElementType);
        Assert.False(innerFn.IsNullable);
        Assert.Equal("int", innerFn.ReturnType.Name);
    }

    [Fact]
    public void NullableFunction_GroupingPrimaryStillWorks() {
        // (fn(): int)? — D-326's nullable-function form still works under the generalised grouping primary
        CompilationUnit unit = ParseOk("fn f(action: (fn(): int)?): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef fnType = Assert.IsType<FunctionTypeRef>(fn.Parameters[0].Type);
        Assert.True(fnType.IsNullable);
        Assert.Equal("int", fnType.ReturnType.Name);
    }

    // ------------------------------------------------------------------
    // Nesting (D-182)
    // ------------------------------------------------------------------

    [Fact]
    public void ArrayTypeRef_TwoDimensional_Parses() {
        // int[][] — array of arrays of int
        CompilationUnit unit = ParseOk("fn f(xs: int[][]): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        ArrayTypeRef outer = Assert.IsType<ArrayTypeRef>(fn.Parameters[0].Type);
        Assert.False(outer.IsNullable);
        ArrayTypeRef inner = Assert.IsType<ArrayTypeRef>(outer.ElementType);
        Assert.Equal("int", inner.ElementType.Name);
        Assert.False(inner.IsNullable);
    }

    [Fact]
    public void ArrayTypeRef_ThreeDimensional_Parses() {
        // int[][][] — array of arrays of arrays of int
        CompilationUnit unit = ParseOk("fn f(xs: int[][][]): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        ArrayTypeRef outermost = Assert.IsType<ArrayTypeRef>(fn.Parameters[0].Type);
        ArrayTypeRef middle = Assert.IsType<ArrayTypeRef>(outermost.ElementType);
        ArrayTypeRef innermost = Assert.IsType<ArrayTypeRef>(middle.ElementType);
        Assert.Equal("int", innermost.ElementType.Name);
    }

    // ------------------------------------------------------------------
    // D-326 × D-327 interaction — makeCounter-adjacent
    // ------------------------------------------------------------------

    [Fact]
    public void FunctionReturningArrayOfInts_ParsesAndNests() {
        // fn counter(): fn(): int[] — a named fn whose return is a function returning int[]
        CompilationUnit unit = ParseOk("""
            fn counter(): fn(): int[] {
                return () => []
            }
            """);
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef fnType = Assert.IsType<FunctionTypeRef>(fn.ReturnType);
        ArrayTypeRef arr = Assert.IsType<ArrayTypeRef>(fnType.ReturnType);
        Assert.Equal("int", arr.ElementType.Name);
        Assert.False(arr.IsNullable);
    }

    [Fact]
    public void ArrayOfFunctionType_FunctionReturnAnnotation_Parses() {
        // fn arrOfCounters(): (fn(): int)[] — a named fn returning an array of functions
        CompilationUnit unit = ParseOk("""
            fn arrOfCounters(): (fn(): int)[] {
                return []
            }
            """);
        FnDecl fn = Single<FnDecl>(unit);
        ArrayTypeRef arr = Assert.IsType<ArrayTypeRef>(fn.ReturnType);
        Assert.False(arr.IsNullable);
        FunctionTypeRef innerFn = Assert.IsType<FunctionTypeRef>(arr.ElementType);
        Assert.Equal("int", innerFn.ReturnType.Name);
    }

    // ------------------------------------------------------------------
    // Negative / recovery
    // ------------------------------------------------------------------

    [Fact]
    public void ArrayTypeRef_UnterminatedBracket_IsE2001() {
        // xs: int[ := [] — missing ] before :=
        (_, DiagnosticBag bag) = Parse("xs: int[ := []\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
    }

    [Fact]
    public void ArrayTypeRef_UnterminatedBracket_DoesNotCascade() {
        // Recovery: after E2001 the parser syncs to the next top-level keyword
        // ('fn' is a sync anchor regardless of bracket depth) and parses it cleanly.
        string src = "xs: int[ := []\nfn ok(): int { return 0 }\n";
        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);
        Assert.Single(bag.Errors);
        // Second declaration was recovered and parsed (not absorbed into the error node)
        Assert.Equal(2, unit.TopLevel.Count);
        Assert.IsType<FnDecl>(unit.TopLevel[1]);
    }

    [Fact]
    public void ArrayTypeRef_FixedSize_IsE2001() {
        // xs: int[5] — fixed-size array not supported; ] expected at position of '5'
        (_, DiagnosticBag bag) = Parse("xs: int[5] := []\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
    }

    [Fact]
    public void ArrayTypeRef_FixedSize_DoesNotCascade() {
        // Recovery after int[5]: the ] in [5] closes bracket depth back to 0 before the
        // newline, so the newline IS a sync anchor and the next statement parses cleanly.
        string src = "xs: int[5] := []\nok := 1\n";
        (CompilationUnit unit, DiagnosticBag bag) = Parse(src);
        Assert.Single(bag.Errors);
        Assert.Equal(2, unit.TopLevel.Count);
        Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
    }
}
