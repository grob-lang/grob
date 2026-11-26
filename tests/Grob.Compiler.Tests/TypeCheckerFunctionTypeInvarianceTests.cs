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
}
