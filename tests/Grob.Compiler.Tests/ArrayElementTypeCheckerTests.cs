using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>TypeChecker tests for array element-type tracking (D-351).</summary>
public sealed class ArrayElementTypeCheckerTests {
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
    // Array literal inference.
    // ------------------------------------------------------------------

    [Fact]
    public void IntArrayLiteral_InfersIntElementType_RejectsStringOnAssignment() {
        // Proves [1, 2, 3] carries a real int element type, not just the bare Array tag:
        // assigning it to a string[]-annotated binding is now caught.
        DiagnosticBag bag = Check("items: string[] := [1, 2, 3]\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
    }

    [Fact]
    public void IntArrayLiteral_AssignedToIntArrayAnnotation_NoDiagnostics() {
        DiagnosticBag bag = Check("items: int[] := [1, 2, 3]\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void HeterogeneousArrayLiteral_IntAndString_ReportsE0001() {
        DiagnosticBag bag = Check("items := [1, \"a\"]\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
    }

    [Fact]
    public void HeterogeneousArrayLiteral_IntAndFloat_Widens_NoDiagnostics() {
        // int/float mixing widens to float, mirroring UnifyTernaryArms elsewhere in the
        // corpus (ternary arms, switch-expression arms) — the one documented widening rule.
        DiagnosticBag bag = Check("items: float[] := [1, 2.5]\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Function parameter / argument enforcement.
    // ------------------------------------------------------------------

    [Fact]
    public void FunctionTakingIntArray_RejectsStringArrayArgument() {
        DiagnosticBag bag = Check("""
            fn f(xs: int[]): int { return 0 }
            strs: string[] := ["a", "b"]
            f(strs)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0004", diag.Code);
    }

    [Fact]
    public void FunctionTakingIntArray_AcceptsIntArrayArgument() {
        DiagnosticBag bag = Check("""
            fn f(xs: int[]): int { return 0 }
            nums: int[] := [1, 2, 3]
            f(nums)
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void FunctionTakingIntArray_AcceptsIntArrayLiteralArgument() {
        DiagnosticBag bag = Check("""
            fn f(xs: int[]): int { return 0 }
            f([1, 2, 3])
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Index read.
    // ------------------------------------------------------------------

    [Fact]
    public void IndexRead_OnIntArray_ResolvesToIntElementType() {
        // arr[0] used where a string is expected on an int[] is now a real type error —
        // proving VisitIndex no longer returns unconditional Unknown.
        DiagnosticBag bag = Check("""
            nums: int[] := [1, 2, 3]
            s: string := nums[0]
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
    }

    [Fact]
    public void IndexRead_OnIntArray_AssignableToInt_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            nums: int[] := [1, 2, 3]
            n: int := nums[0]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Index write — closes the A2 gap.
    // ------------------------------------------------------------------

    [Fact]
    public void IndexWrite_StringOnIntArray_ReportsE0001() {
        DiagnosticBag bag = Check("""
            nums: int[] := [1, 2, 3]
            nums[0] = "x"
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
    }

    [Fact]
    public void IndexWrite_IntOnIntArray_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            nums: int[] := [1, 2, 3]
            nums[0] = 42
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // for...in.
    // ------------------------------------------------------------------

    [Fact]
    public void ForIn_OverIntArrayLiteral_TypesItemAsInt() {
        DiagnosticBag bag = Check("""
            sum: string := ""
            for x in [1, 2, 3] {
                sum = x
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
    }

    [Fact]
    public void ForIn_OverIntArray_ArithmeticOnItem_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            nums: int[] := [1, 2, 3]
            sum := 0
            for x in nums {
                sum = sum + x
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Struct fields.
    // ------------------------------------------------------------------

    [Fact]
    public void StructConstruction_WrongArrayElementTypeForField_ReportsE0001() {
        DiagnosticBag bag = Check("""
            type T { tags: int[] }
            t := T { tags: ["a", "b"] }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
    }

    [Fact]
    public void StructConstruction_CorrectArrayElementTypeForField_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            type T { tags: int[] }
            t := T { tags: [1, 2] }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // ------------------------------------------------------------------
    // Distinct types, §3.1.1.
    // ------------------------------------------------------------------

    [Fact]
    public void IntArray_AssignedToStringArrayAnnotation_ReportsE0001() {
        DiagnosticBag bag = Check("""
            nums: int[] := [1, 2, 3]
            strs: string[] := nums
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
    }

    // ------------------------------------------------------------------
    // Nested arrays (T[][]) — the recursive element-descriptor branch.
    // ------------------------------------------------------------------

    [Fact]
    public void NestedIntArrayLiteral_AssignedToStringNestedArrayAnnotation_ReportsE0001() {
        DiagnosticBag bag = Check("matrix: string[][] := [[1, 2], [3, 4]]\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
    }

    [Fact]
    public void NestedIntArrayLiteral_AssignedToIntNestedArrayAnnotation_NoDiagnostics() {
        DiagnosticBag bag = Check("matrix: int[][] := [[1, 2], [3, 4]]\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void IndexNode_HasNoResolvedTypeOrDeclaration_PerD348() {
        // §3.1.1 is scoped to identifier nodes (plus StructConstructionExpr/SwitchExprNode);
        // IndexExpr carries neither property, per D-348 — element typing does not extend
        // the invariant there.
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            nums: int[] := [1, 2, 3]
            n: int := nums[0]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        VarDeclStmt indexDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        Assert.IsType<IndexExpr>(indexDecl.Initializer);
    }
}
