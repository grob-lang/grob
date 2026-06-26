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
}
