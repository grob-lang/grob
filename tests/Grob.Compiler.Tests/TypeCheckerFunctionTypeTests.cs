using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>TypeChecker tests for function-type annotations and structural assignability (D-326).</summary>
public sealed class TypeCheckerFunctionTypeTests {
    // -----------------------------------------------------------------------
    // Helper — mirrors TypeCheckerTests.TypeCheckSource
    // -----------------------------------------------------------------------

    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag);
        checker.Check(unit);
        return (unit, bag);
    }

    // -----------------------------------------------------------------------
    // Structural assignability — variable declaration binding
    // -----------------------------------------------------------------------

    /// <summary><c>x: fn(): int := () => 1</c> — identical signature is assignable; no error.</summary>
    [Fact]
    public void FunctionType_IdenticalSignature_IsAssignable() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: fn(): int := () => 1\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// <c>readonly f: fn(): int := () => 1</c> — a readonly binding with a function-type
    /// annotation must carry the lambda descriptor through binding, so an identical
    /// signature is assignable; no spurious error (Fix G).
    /// </summary>
    [Fact]
    public void ReadonlyFunctionType_IdenticalSignature_IsAssignable() {
        (_, DiagnosticBag bag) = TypeCheckSource("readonly f: fn(): int := () => 1\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// <c>readonly f: fn(): int = () => "s"</c> — return-type mismatch on a readonly
    /// binding still emits E0001 at the lambda (Fix G keeps the negative path).
    /// </summary>
    [Fact]
    public void ReadonlyFunctionType_DifferentReturnType_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("readonly f: fn(): int := () => \"s\"\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
    }

    /// <summary><c>x: fn(): int := () => "s"</c> — return-type mismatch emits E0001 at the lambda.</summary>
    [Fact]
    public void FunctionType_DifferentReturnType_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: fn(): int := () => \"s\"\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(17, d.Range.Start.Column);
    }

    /// <summary><c>x: fn(int): int := () => 1</c> — arity mismatch emits E0001 at the lambda.</summary>
    [Fact]
    public void FunctionType_DifferentArity_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: fn(int): int := () => 1\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(20, d.Range.Start.Column);
    }

    /// <summary><c>x: (fn(): int)? := () => 1</c> — nullable widening is assignable; no error.</summary>
    [Fact]
    public void FunctionType_NullableWidening_IsAssignable() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: (fn(): int)? := () => 1\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Full pipeline — makeCounter canonical example
    // -----------------------------------------------------------------------

    /// <summary>The full <c>makeCounter</c> function with return type <c>fn(): int</c> type-checks without errors.</summary>
    [Fact]
    public void MakeCounter_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn makeCounter(): fn(): int {
                count := 0
                return () => {
                    count = count + 1
                    count
                }
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Higher-order parameter annotation
    // -----------------------------------------------------------------------

    /// <summary>A function with a <c>fn(int): int</c> parameter annotation type-checks without errors.</summary>
    [Fact]
    public void HigherOrderParam_FnAnnotation_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn applyOne(action: fn(int): int): void { }\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// A higher-order parameter default whose lambda signature does not match the
    /// parameter's function type emits E0004 — the default must be checked with the
    /// descriptor-aware assignability rule (Fix H).
    /// </summary>
    [Fact]
    public void HigherOrderParamDefault_WrongReturnType_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn f(action: fn(): int = () => \"s\"): void { }\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
    }

    /// <summary>
    /// A higher-order parameter default whose lambda signature matches the parameter's
    /// function type type-checks without error (Fix H keeps the positive path).
    /// </summary>
    [Fact]
    public void HigherOrderParamDefault_MatchingSignature_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn f(action: fn(): int = () => 1): void { }\n");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// Passing a lambda whose signature does not match a function-type parameter emits
    /// E0004 — the argument must be checked with the descriptor-aware rule, not merely
    /// as fn-to-fn (D-326; Fix J).
    /// </summary>
    [Fact]
    public void HigherOrderArgument_WrongSignature_IsE0004() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn applyOne(action: fn(int): int): void { }
            applyOne(() => "s")
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
    }

    /// <summary>
    /// Passing a lambda whose signature matches a function-type parameter type-checks
    /// without error (Fix J keeps the positive path).
    /// </summary>
    [Fact]
    public void HigherOrderArgument_MatchingSignature_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn applyOne(action: fn(int): int): void { }
            applyOne((n) => n + 1)
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Call-result function type flows to a binding annotation (Fix I)
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>c: fn(): int := makeCounter()</c> where <c>makeCounter</c> returns <c>fn(): int</c>
    /// must type-check: the call's result type and descriptor flow to the annotated
    /// binding, so the matching annotation is assignable (Fix I).
    /// </summary>
    [Fact]
    public void CallReturningFunctionType_MatchingAnnotation_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn makeCounter(): fn(): int {
                count := 0
                return () => {
                    count = count + 1
                    count
                }
            }
            readonly c: fn(): int := makeCounter()
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    /// <summary>
    /// <c>c: fn(): string := makeCounter()</c> where <c>makeCounter</c> returns
    /// <c>fn(): int</c> must emit E0001: the call's descriptor does not match the
    /// annotation (Fix I keeps the negative path).
    /// </summary>
    [Fact]
    public void CallReturningFunctionType_MismatchedAnnotation_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn makeCounter(): fn(): int {
                count := 0
                return () => {
                    count = count + 1
                    count
                }
            }
            readonly c: fn(): string := makeCounter()
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
    }

    // -----------------------------------------------------------------------
    // Return-type mismatch (E0005)
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>fn f(): fn(): int { return () => "s" }</c> — the lambda's body type (string)
    /// does not match the declared return type (<c>fn(): int</c>); emits E0005 at the lambda.
    /// </summary>
    [Fact]
    public void FunctionType_ReturnMismatch_IsE0005() {
        (_, DiagnosticBag bag) = TypeCheckSource("fn f(): fn(): int { return () => \"s\" }\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0005", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(28, d.Range.Start.Column);
    }

    /// <summary>
    /// Returning a bound variable whose function type does not match the declared return
    /// type emits E0005 — a non-lambda return value is checked against the symbol's
    /// descriptor, not merely fn-to-fn (D-326; Fix K).
    /// </summary>
    [Fact]
    public void FunctionType_ReturnBoundVariableMismatch_IsE0005() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn f(): fn(): int {
                g: fn(): string := () => "s"
                return g
            }
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0005", d.Code);
    }

    /// <summary>
    /// Returning a bound variable whose function type matches the declared return type
    /// type-checks without error (Fix K keeps the positive path).
    /// </summary>
    [Fact]
    public void FunctionType_ReturnBoundVariableMatch_TypeChecks() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn f(): fn(): int {
                g: fn(): int := () => 1
                return g
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Descriptor-to-descriptor (annotation-to-annotation) structural identity
    // -----------------------------------------------------------------------

    /// <summary>
    /// Binding a <c>fn(int): int</c> variable to a <c>fn(string): int</c> annotation emits
    /// E0001 — a concrete parameter-type mismatch fails invariant structural assignability.
    /// </summary>
    [Fact]
    public void FunctionDescriptors_ParameterTypeMismatch_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            g: fn(int): int := (n) => n + 1
            f: fn(string): int := g
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
    }

    /// <summary>
    /// Binding a <c>fn(fn(): int): int</c> variable to a <c>fn(fn(): string): int</c>
    /// annotation emits E0001 — a nested parameter descriptor mismatch fails invariant
    /// structural assignability through recursion.
    /// </summary>
    [Fact]
    public void FunctionDescriptors_NestedParameterMismatch_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("""
            fn takesIntReturner(p: fn(): int): int { return 0 }
            g: fn(fn(): int): int := takesIntReturner
            f: fn(fn(): string): int := g
            """);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
    }

    /// <summary>
    /// Binding a non-function value (<c>5</c>) to a function-type annotation emits E0001 —
    /// the descriptor-aware overload falls back to the plain rule when one side is not a
    /// function type.
    /// </summary>
    [Fact]
    public void FunctionAnnotation_NonFunctionValue_IsE0001() {
        (_, DiagnosticBag bag) = TypeCheckSource("x: fn(): int := 5\n");
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0001", d.Code);
    }

    // -----------------------------------------------------------------------
    // Nested function-type structural identity (D-326, Fix F)
    //
    // A FunctionTypeDescriptor for a function-typed parameter or return must carry
    // the nested descriptor, otherwise two differently-shaped nested function types
    // (fn(fn(): int): int vs fn(fn(): string): int) both collapse to a flat
    // [GrobType.Function] parameter and compare equal. These tests assert the
    // descriptor's structural-equality contract directly.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Two descriptors that differ only in a nested parameter descriptor's return type
    /// must compare unequal — a flat <c>GrobType.Function</c> parameter would collapse them.
    /// </summary>
    [Fact]
    public void NestedFunctionType_DifferentInnerReturn_DescriptorsNotEqual() {
        FunctionTypeDescriptor innerReturnsInt = new([], GrobType.Int);
        FunctionTypeDescriptor innerReturnsString = new([], GrobType.String);

        FunctionTypeDescriptor a = new(
            [GrobType.Function], GrobType.Int, [innerReturnsInt], null);
        FunctionTypeDescriptor b = new(
            [GrobType.Function], GrobType.Int, [innerReturnsString], null);

        Assert.NotEqual(a, b);
    }

    /// <summary>
    /// Two descriptors with identical nested shape compare equal and hash equally, so the
    /// same nested annotation on both sides of an assignment is assignable.
    /// </summary>
    [Fact]
    public void NestedFunctionType_SameShape_DescriptorsEqual() {
        FunctionTypeDescriptor innerA = new([GrobType.Int], GrobType.Bool);
        FunctionTypeDescriptor innerB = new([GrobType.Int], GrobType.Bool);

        FunctionTypeDescriptor a = new(
            [GrobType.Function], GrobType.Int, [innerA], null);
        FunctionTypeDescriptor b = new(
            [GrobType.Function], GrobType.Int, [innerB], null);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    /// <summary>
    /// A differing nested return descriptor also breaks equality.
    /// </summary>
    [Fact]
    public void NestedFunctionType_DifferentReturnDescriptor_DescriptorsNotEqual() {
        FunctionTypeDescriptor returnsIntReturner = new([], GrobType.Int);
        FunctionTypeDescriptor returnsStringReturner = new([], GrobType.String);

        FunctionTypeDescriptor a = new(
            [], GrobType.Function, [], returnsIntReturner);
        FunctionTypeDescriptor b = new(
            [], GrobType.Function, [], returnsStringReturner);

        Assert.NotEqual(a, b);
    }
}
