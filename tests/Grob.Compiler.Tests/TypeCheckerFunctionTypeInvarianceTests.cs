using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Function types are compared invariantly at every parameter and return position
/// (D-326, D-461, R-97): <c>fn(int): int</c> is assignable only to <c>fn(int): int</c>, so
/// the <c>int</c> to <c>float</c> widening a plain value gets is refused inside a function
/// type, in both directions. An <see cref="GrobType.Unknown"/> position, as an inferred
/// lambda carries, stays permissive.
/// </summary>
public sealed class TypeCheckerFunctionTypeInvarianceTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag, SharedCatalogue.Stdlib);
        checker.Check(unit);
        return (unit, bag);
    }

    private static void AssertSingleError(string source, string code, int line, int column) {
        (_, DiagnosticBag bag) = TypeCheckSource(source);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal(code, d.Code);
        Assert.Equal(line, d.Range.Start.Line);
        Assert.Equal(column, d.Range.Start.Column);
    }

    private static void AssertNoErrors(string source) {
        (_, DiagnosticBag bag) = TypeCheckSource(source);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Binding: both directions, each position
    // -----------------------------------------------------------------------

    [Fact]
    public void Binding_IntFunctionToFloatFunction_IsE0001() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            h: fn(float): float := g
            """, "E0001", 2, 24);

    [Fact]
    public void Binding_FloatFunctionToIntFunction_IsE0001() =>
        AssertSingleError("""
            g: fn(float): float := (n) => n * 2.0
            h: fn(int): int := g
            """, "E0001", 2, 20);

    [Fact]
    public void Binding_ParameterOnlyDiffers_IntToFloat_IsE0001() =>
        AssertSingleError("""
            g: fn(int): float := (n) => 1.5
            h: fn(float): float := g
            """, "E0001", 2, 24);

    [Fact]
    public void Binding_ReturnOnlyDiffers_IntToFloat_IsE0001() =>
        AssertSingleError("""
            g: fn(float): int := (n) => 1
            h: fn(float): float := g
            """, "E0001", 2, 24);

    [Fact]
    public void Binding_ParameterOnlyDiffers_FloatToInt_IsE0001() =>
        AssertSingleError("""
            g: fn(float): int := (n) => 1
            h: fn(int): int := g
            """, "E0001", 2, 20);

    [Fact]
    public void Binding_ReturnOnlyDiffers_FloatToInt_IsE0001() =>
        AssertSingleError("""
            g: fn(int): float := (n) => 1.5
            h: fn(int): int := g
            """, "E0001", 2, 20);

    [Fact]
    public void Binding_SecondParameterDiffers_IsE0001() =>
        AssertSingleError("""
            g: fn(string, int): int := (s, n) => n
            h: fn(string, float): int := g
            """, "E0001", 2, 30);

    [Fact]
    public void Binding_ParameterOfFunctionTypedParameter_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(int): int): void {
                h: fn(float): float := g
            }
            """, "E0001", 2, 28);

    [Fact]
    public void Readonly_IntFunctionToFloatFunction_IsE0001() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            readonly h: fn(float): float := g
            """, "E0001", 2, 33);

    // -----------------------------------------------------------------------
    // Nullable: the slot widens, the positions do not
    // -----------------------------------------------------------------------

    [Fact]
    public void Binding_IntFunctionToNullableFloatFunction_IsE0001() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            h: (fn(float): float)? := g
            """, "E0001", 2, 27);

    [Fact]
    public void Binding_NullableIntFunctionToNullableFloatFunction_IsE0001() =>
        AssertSingleError("""
            g: (fn(int): int)? := (n) => n + 1
            h: (fn(float): float)? := g
            """, "E0001", 2, 27);

    [Fact]
    public void Binding_NonNullableParameterToNullableParameter_IsE0001() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            h: fn(int?): int := g
            """, "E0001", 2, 21);

    [Fact]
    public void Binding_NonNullableReturnToNullableReturn_IsE0001() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            h: fn(int): int? := g
            """, "E0001", 2, 21);

    [Fact]
    public void Binding_NullableParameterToNonNullableParameter_IsE0001() =>
        AssertSingleError("""
            g: fn(int?): int := (n) => 1
            h: fn(int): int := g
            """, "E0001", 2, 20);

    [Fact]
    public void Binding_FunctionToNullableSameFunction_IsAccepted() =>
        AssertNoErrors("""
            g: fn(float): float := (n) => n * 2.0
            h: (fn(float): float)? := g
            """);

    // -----------------------------------------------------------------------
    // Assignment, argument, return, default and struct field
    // -----------------------------------------------------------------------

    [Fact]
    public void Assignment_IntFunctionToFloatFunction_IsE0001() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            h: fn(float): float := (n) => n * 2.0
            h = g
            """, "E0001", 3, 5);

    [Fact]
    public void Assignment_FloatFunctionToIntFunction_IsE0001() =>
        AssertSingleError("""
            g: fn(float): float := (n) => n * 2.0
            h: fn(int): int := (n) => n + 1
            h = g
            """, "E0001", 3, 5);

    [Fact]
    public void Argument_IntFunctionToFloatFunction_IsE0004() =>
        AssertSingleError("""
            fn take(f: fn(float): float): void { }
            g: fn(int): int := (n) => n + 1
            take(g)
            """, "E0004", 3, 6);

    [Fact]
    public void Argument_FloatFunctionToIntFunction_IsE0004() =>
        AssertSingleError("""
            fn take(f: fn(int): int): void { }
            g: fn(float): float := (n) => n * 2.0
            take(g)
            """, "E0004", 3, 6);

    [Fact]
    public void Argument_ThroughFunctionValue_IntFunctionToFloatFunction_IsE0004() =>
        AssertSingleError("""
            take: fn(fn(float): float): void := (f) => { }
            g: fn(int): int := (n) => n + 1
            take(g)
            """, "E0004", 3, 6);

    [Fact]
    public void Return_IntFunctionToFloatFunction_IsE0005() =>
        AssertSingleError("""
            fn widen(g: fn(int): int): fn(float): float {
                return g
            }
            """, "E0005", 2, 12);

    [Fact]
    public void Return_FloatFunctionToIntFunction_IsE0005() =>
        AssertSingleError("""
            fn narrow(g: fn(float): float): fn(int): int {
                return g
            }
            """, "E0005", 2, 12);

    [Fact]
    public void ParameterDefault_IntFunctionToFloatFunction_IsE0004() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            fn take(f: fn(float): float = g): void { }
            """, "E0004", 2, 31);

    [Fact]
    public void StructField_IntFunctionToFloatFunction_IsE0001() =>
        AssertSingleError("""
            type H {
                f: fn(float): float
            }
            g: fn(int): int := (n) => n + 1
            h := H { f: g }
            """, "E0001", 5, 13);

    [Fact]
    public void StructFieldAssignment_IntFunctionToFloatFunction_IsE0001() =>
        AssertSingleError("""
            type H {
                f: fn(float): float
            }
            g: fn(int): int := (n) => n + 1
            h := H { f: (n) => n * 2.0 }
            h.f = g
            """, "E0001", 6, 7);

    // -----------------------------------------------------------------------
    // Nested function types
    // -----------------------------------------------------------------------

    [Fact]
    public void Nested_FunctionTypedParameterDiffers_IsE0001() =>
        AssertSingleError("""
            g: fn(fn(int): int): void := (f) => { }
            h: fn(fn(float): float): void := g
            """, "E0001", 2, 34);

    [Fact]
    public void Nested_FunctionTypedParameterDiffers_FloatToInt_IsE0001() =>
        AssertSingleError("""
            g: fn(fn(float): float): void := (f) => { }
            h: fn(fn(int): int): void := g
            """, "E0001", 2, 30);

    [Fact]
    public void Nested_FunctionTypedReturnDiffers_IsE0001() =>
        AssertSingleError("""
            g: fn(): fn(int): int := () => (n) => n + 1
            h: fn(): fn(float): float := g
            """, "E0001", 2, 30);

    [Fact]
    public void Nested_DeeplyNestedPositionDiffers_IsE0001() =>
        AssertSingleError("""
            g: fn(fn(fn(int): int): void): void := (f) => { }
            h: fn(fn(fn(float): int): void): void := g
            """, "E0001", 2, 42);

    // -----------------------------------------------------------------------
    // Positive: what must still be accepted
    // -----------------------------------------------------------------------

    [Fact]
    public void Binding_ExactMatch_IsAccepted() =>
        AssertNoErrors("""
            g: fn(int): int := (n) => n + 1
            h: fn(int): int := g
            """);

    [Fact]
    public void Binding_ExactMatchFloat_IsAccepted() =>
        AssertNoErrors("""
            g: fn(float): float := (n) => n * 2.0
            h: fn(float): float := g
            """);

    [Fact]
    public void Binding_ExactMatchMultipleParametersAndNested_IsAccepted() =>
        AssertNoErrors("""
            g: fn(string, fn(int): float, float?): int? := (s, f, x) => nil
            h: fn(string, fn(int): float, float?): int? := g
            """);

    [Fact]
    public void Binding_LambdaLiteralReturningNil_ToNullableReturn_IsAccepted() =>
        AssertNoErrors("""
            g: fn(string, fn(int): float, float?): int? := (s, f, x) => nil
            """);

    [Fact]
    public void Binding_LambdaLiteralReturningValue_ToNullableReturn_IsAccepted() =>
        AssertNoErrors("""
            g: fn(): int? := () => 1
            """);

    [Fact]
    public void Argument_LambdaLiteralReturningNil_ToNullableReturn_IsAccepted() =>
        AssertNoErrors("""
            fn take(f: fn(): string?): void { }
            take(() => nil)
            """);

    [Fact]
    public void Binding_LambdaLiteralReturningLambda_ToFunctionReturn_IsAccepted() =>
        AssertNoErrors("""
            g: fn(): fn(int): int := () => (n) => 1
            """);

    [Fact]
    public void Binding_LambdaLiteralReturningLambdaOfWrongType_StillRefused() =>
        AssertSingleError("""
            g: fn(): fn(int): int := () => (n) => "s"
            """, "E0001", 1, 26);

    [Fact]
    public void Binding_LambdaLiteralReturningLambda_ToValueReturn_StillRefused() =>
        AssertSingleError("""
            g: fn(): int := () => (n) => 1
            """, "E0001", 1, 17);

    [Fact]
    public void Binding_LambdaLiteralReturningWrongType_StillRefused() =>
        AssertSingleError("""
            g: fn(): int? := () => "s"
            """, "E0001", 1, 18);

    [Fact]
    public void Binding_InferredLambdaToConcreteFunction_IsAccepted() =>
        AssertNoErrors("""
            h: fn(float): float := (n) => n * 2.0
            k: fn(int): int := (n) => n + 1
            """);

    [Fact]
    public void Argument_InferredLambdaToFloatFunction_IsAccepted() =>
        AssertNoErrors("""
            fn take(f: fn(float): float): void { }
            take((n) => n * 2.0)
            """);

    [Fact]
    public void Argument_IntLiteralToFloatFunctionParameter_StillWidens() =>
        AssertNoErrors("""
            h: fn(float): float := (n) => n * 2.0
            r := h(2)
            """);

    [Fact]
    public void Argument_IntBindingToFloatFunctionParameter_StillWidens() =>
        AssertNoErrors("""
            h: fn(float): float := (n) => n * 2.0
            i := 2
            r := h(i)
            """);

    [Fact]
    public void Argument_FloatFunctionValueTakesAnIntThroughANestedCall_StillWidens() =>
        AssertNoErrors("""
            fn apply(f: fn(float): float): float {
                return f(3)
            }
            r := apply((n) => n * 2.0)
            """);

    [Fact]
    public void Binding_ExactMatchNullableFunctionParameter_IsAccepted() =>
        AssertNoErrors("""
            g: fn((fn(): int)?): void := (f) => { }
            h: fn((fn(): int)?): void := g
            """);

    [Fact]
    public void Assignment_DeclaredFunctionToInferredLambdaBinding_IsAccepted() =>
        AssertNoErrors("""
            fn id(g: fn(int): int): fn(int): int { return g }
            k := (n) => n
            k = id((n) => n + 1)
            """);

    // -----------------------------------------------------------------------
    // Provenance of an inferred return follows what a binding holds (D-461)
    // -----------------------------------------------------------------------

    [Fact]
    public void Binding_OriginalLambdaToFloatReturn_KeepsInferredReturnAndIsAccepted() =>
        AssertNoErrors("""
            k := () => 1
            h: fn(): float := k
            """);

    [Fact]
    public void Binding_ReassignedWithAnotherLambda_KeepsInferredReturnAndIsAccepted() =>
        AssertNoErrors("""
            k := () => 1
            k = () => 2
            h: fn(): float := k
            """);

    [Fact]
    public void Binding_ReassignedWithDeclaredReturnFunction_LosesInferredReturn() =>
        AssertSingleError("""
            fn id(f: fn(): int): fn(): int { return f }
            k := () => 1
            k = id(() => 2)
            h: fn(): float := k
            """, "E0001", 4, 19);

    [Fact]
    public void Binding_DeclaredReturnThenAnotherLambda_StaysDeclared() =>
        AssertSingleError("""
            fn id(f: fn(): int): fn(): int { return f }
            k := () => 1
            k = id(() => 2)
            k = () => 3
            h: fn(): float := k
            """, "E0001", 5, 19);

    [Fact]
    public void Binding_ReassignedWithDeclaredReturnFunction_LeavesAnAliasTakenBeforeIt() =>
        AssertNoErrors("""
            fn id(f: fn(): int): fn(): int { return f }
            k := () => 1
            j := k
            k = id(() => 2)
            h: fn(): float := j
            """);

    [Fact]
    public void Binding_ReassignedWithTernaryOfLambdaAndDeclaredReturn_LosesInferredReturn() =>
        AssertSingleError("""
            fn id(f: fn(): int): fn(): int { return f }
            c := true
            k := () => 1
            k = c ? () => 2 : id(() => 3)
            h: fn(): float := k
            """, "E0001", 5, 19);

    [Fact]
    public void Binding_ReassignedWithDeclaredReturnFunction_LosesInferredReturnForAnArgumentToo() =>
        AssertSingleError("""
            fn id(f: fn(): int): fn(): int { return f }
            fn take(f: fn(): float): void { }
            k := () => 1
            k = id(() => 2)
            take(k)
            """, "E0004", 5, 6);

    [Fact]
    public void Binding_RefusedAssignment_KeepsInferredReturnAndAddsNoSecondError() =>
        AssertSingleError("""
            fn id(f: fn(): string): fn(): string { return f }
            k := () => 1
            k = id(() => "s")
            h: fn(): float := k
            """, "E0001", 3, 5);

    [Fact]
    public void Binding_ReassignedWithLambdaReturningADeclaredFunction_LosesInferredReturnOneLevelDown() =>
        AssertSingleError("""
            fn make(): fn(): int { return () => 2 }
            k := () => () => 1
            k = () => make()
            h: fn(): fn(): float := k
            """, "E0001", 4, 25);

    [Fact]
    public void Binding_ReassignedWithLambdaOfUntrackedReturn_KeepsNestedInferredReturn() =>
        AssertNoErrors("""
            k := (a) => () => 1
            k = (x) => x
            h: fn(int): fn(): float := k
            """);

    [Fact]
    public void ReadonlyBinding_RefusedReassignment_KeepsInferredReturnAndAddsNoSecondError() =>
        AssertSingleError("""
            fn id(f: fn(): int): fn(): int { return f }
            readonly k := () => 1
            k = id(() => 2)
            h: fn(): float := k
            """, "E0202", 3, 1);

    [Fact]
    public void Binding_NestedLambdaToNestedFloatReturn_KeepsInferredReturnAndIsAccepted() =>
        AssertNoErrors("""
            k := () => () => 1
            k = () => () => 2
            h: fn(): fn(): float := k
            """);

    [Fact]
    public void Binding_ReassignedWithNestedDeclaredReturn_LosesInferredReturnAtEveryLevel() =>
        AssertSingleError("""
            fn id(f: fn(): fn(): int): fn(): fn(): int { return f }
            k := () => () => 1
            k = id(() => () => 2)
            h: fn(): fn(): float := k
            """, "E0001", 4, 25);

    [Fact]
    public void Binding_ReassignedWithNestedDeclaredReturn_LeavesANestedAliasTakenBeforeIt() =>
        AssertNoErrors("""
            fn id(f: fn(): fn(): int): fn(): fn(): int { return f }
            k := () => () => 1
            j := k
            k = id(() => () => 2)
            h: fn(): fn(): float := j
            """);

    [Fact]
    public void Binding_TernaryOfNestedLambdasReassignedWithNestedDeclaredReturn_LosesInferredReturnAtEveryLevel() =>
        AssertSingleError("""
            fn id(f: fn(): fn(): int): fn(): fn(): int { return f }
            c := true
            k := c ? () => () => 1 : () => () => 2
            k = id(() => () => 3)
            h: fn(): fn(): float := k
            """, "E0001", 5, 25);

    [Fact]
    public void Binding_CombinedShapeWithAReturnBound_LosesInferredReturnAtEveryLevel() =>
        AssertSingleError("""
            fn id(f: fn(int): fn(): int): fn(int): fn(): int { return f }
            c := true
            k := c ? (x) => x : (y) => () => 1
            k = id((z) => () => 2)
            h: fn(int): fn(): float := k
            """, "E0001", 5, 28);

    [Fact]
    public void Binding_CombinedShapeWithAReturnBound_KeepsInferredReturnWhileOnlyLambdasAreAssigned() =>
        AssertNoErrors("""
            c := true
            k := c ? (x) => x : (y) => () => 1
            k = (z) => () => 2
            h: fn(int): fn(): float := k
            """);

    // D-461 records this limit, which R-96 retires: the checker keeps no flow and walks in
    // source order, so a reassignment in a function body it reaches after a read is not seen
    // at that read. 'use' reads k before 'reset' is reached, so k is still the lambda there.
    [Fact]
    public void Binding_ReassignedInALaterFunctionBody_IsNotSeenAtAnEarlierRead() =>
        AssertNoErrors("""
            fn id(f: fn(): int): fn(): int { return f }
            k := () => 1
            fn use(): void {
                h: fn(): float := k
            }
            fn reset(): void {
                k = id(() => 2)
            }
            """);

    [Fact]
    public void StructField_AssignedALambda_IsADeclaredTypeAndRefusedForAFloatReturn() =>
        AssertSingleError("""
            type H {
                f: fn(): int
            }
            s := H { f: () => 1 }
            s.f = () => 2
            h: fn(): float := s.f
            """, "E0001", 6, 19);

    [Fact]
    public void ArrayElement_HoldingALambda_IsNotShownToMatchAFloatReturn() =>
        AssertSingleError("""
            fs := [() => 1]
            h: fn(): float := fs[0]
            """, "E0001", 2, 19);
}
