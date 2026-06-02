using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 3 Increment D — T? nullable type rules.
/// Covers: T? annotations, nil assignability, E0104 (nullable used where
/// non-nullable required), E0101 (nil dereference without '?.' or '??'),
/// '??' type resolution, and '?.' chain result type.
/// </summary>
public sealed class TypeCheckerNullableTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag);
        checker.Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    // -----------------------------------------------------------------------
    // T? type annotations — ResolveTypeRef with IsNullable = true
    // -----------------------------------------------------------------------

    [Fact]
    public void NullableIntAnnotation_NoError_WhenInitialisedWithInt() {
        var diag = Check("x: int? := 42");
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    [Fact]
    public void NullableStringAnnotation_NoError_WhenInitialisedWithString() {
        var diag = Check("x: string? := \"hello\"");
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    [Fact]
    public void NullableIntAnnotation_NoError_WhenInitialisedWithNil() {
        var diag = Check("x: int? := nil");
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    [Fact]
    public void NullableBoolAnnotation_NoError_WhenInitialisedWithBool() {
        var diag = Check("x: bool? := true");
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    [Fact]
    public void NullableFloatAnnotation_NoError_WhenInitialisedWithFloat() {
        var diag = Check("x: float? := 3.14");
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    // -----------------------------------------------------------------------
    // E0104 — nullable used where non-nullable required
    // -----------------------------------------------------------------------

    [Fact]
    public void NullableToNonNullable_Declaration_EmitsE0104() {
        // x: int? = nil; y: int := x  — assigning nullable to non-nullable
        var diag = Check("""
            x: int? := nil
            y: int := x
            """);
        Diagnostic err = Assert.Single(diag.Errors);
        Assert.Equal("E0104", err.Code);
        Assert.Equal((2, 11), (err.Range.Start.Line, err.Range.Start.Column));
    }

    [Fact]
    public void NullableToNonNullable_Assignment_EmitsE0104() {
        var diag = Check("""
            x: int? := nil
            y: int := 0
            y = x
            """);
        Diagnostic err = Assert.Single(diag.Errors);
        Assert.Equal("E0104", err.Code);
        Assert.Equal((3, 5), (err.Range.Start.Line, err.Range.Start.Column));
    }

    [Fact]
    public void NonNullableToNullable_NoError() {
        // int is assignable to int? (non-null into nullable slot)
        var diag = Check("""
            x: int := 5
            y: int? := x
            """);
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    // -----------------------------------------------------------------------
    // nil assignability
    // -----------------------------------------------------------------------

    [Fact]
    public void NilToNonNullable_EmitsError() {
        // Assigning nil to a non-nullable binding is an error.
        var diag = Check("x: int := nil");
        Diagnostic err = Assert.Single(diag.Errors);
        Assert.Equal("E0001", err.Code);
        Assert.Equal((1, 11), (err.Range.Start.Line, err.Range.Start.Column));
    }

    [Fact]
    public void NilToNullableInt_NoError() {
        var diag = Check("x: int? := nil");
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    [Fact]
    public void NilToNullableString_NoError() {
        var diag = Check("x: string? := nil");
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    // -----------------------------------------------------------------------
    // E0101 — nil dereference without ?. or ??
    // -----------------------------------------------------------------------

    [Fact]
    public void DotMemberAccess_OnNullableInt_EmitsE0101() {
        var diag = Check("x: int? := nil\nprint(x.something)");
        Diagnostic err = Assert.Single(diag.Errors);
        Assert.Equal("E0101", err.Code);
        Assert.Equal((2, 7), (err.Range.Start.Line, err.Range.Start.Column));
    }

    [Fact]
    public void DotMemberAccess_OnNullableString_EmitsE0101() {
        var diag = Check("x: string? := \"hi\"\nprint(x.length)");
        Diagnostic err = Assert.Single(diag.Errors);
        Assert.Equal("E0101", err.Code);
        Assert.Equal((2, 7), (err.Range.Start.Line, err.Range.Start.Column));
    }

    [Fact]
    public void QuestionDotMemberAccess_OnNullable_NoE0101() {
        // ?. on a nullable type should not emit E0101.
        var diag = Check("x: int? := nil\nprint(x?.something)");
        Assert.DoesNotContain(diag.Errors, d => d.Code == "E0101");
    }

    [Fact]
    public void DotMemberAccess_OnNonNullable_NoE0101() {
        // '.' on a non-nullable type is fine (struct field — deferred Sprint 5,
        // but the nil check itself should not fire).
        var diag = Check("x: int := 42\nprint(x.something)");
        Assert.DoesNotContain(diag.Errors, d => d.Code == "E0101");
    }

    // -----------------------------------------------------------------------
    // ?? — nil-coalescing type resolution
    // -----------------------------------------------------------------------

    [Fact]
    public void NilCoalesce_NullableIntAndInt_NoError() {
        var diag = Check("""
            x: int? := nil
            y := x ?? 0
            """);
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    [Fact]
    public void NilCoalesce_NullableStringAndString_NoError() {
        var diag = Check("""
            x: string? := nil
            y := x ?? "default"
            """);
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    [Fact]
    public void NilCoalesce_MixedElementTypes_EmitsE0002() {
        // int? ?? string — element types don't match
        var diag = Check("""
            x: int? := nil
            y := x ?? "not-an-int"
            """);
        Diagnostic err = Assert.Single(diag.Errors);
        Assert.Equal("E0002", err.Code);
        Assert.Equal((2, 6), (err.Range.Start.Line, err.Range.Start.Column));
    }

    [Fact]
    public void NilCoalesce_NonNullableOnLeft_NoError() {
        // Non-nullable ?? something is a no-op at runtime; type checker is permissive.
        var diag = Check("""
            x: int := 5
            y := x ?? 0
            """);
        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    // -----------------------------------------------------------------------
    // ?. chain result type — downstream ?? compatibility
    // -----------------------------------------------------------------------

    [Fact]
    public void QuestionDot_ResultIsUnknown_CompatibleWithNilCoalesce() {
        // ?. result type is Unknown (member types deferred Sprint 5).
        // The ?? should not emit a false E0002 for the Unknown type.
        var diag = Check("""
            x: int? := nil
            y := x?.toString ?? "none"
            """);
        // E0101 must NOT fire (optional access); E0002 must NOT fire (Unknown ?? string is permissive).
        Assert.DoesNotContain(diag.Errors, d => d.Code == "E0101");
        Assert.DoesNotContain(diag.Errors, d => d.Code == "E0002");
    }
}
