using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for map-literal construction (D-376) — proving the new
/// <c>MapDescriptorOf</c> literal tier actually works (not merely that the literal
/// parses), the duplicate-key diagnostic (E0016), and value-type checking (E0004) against
/// the descriptor resolved from <c>ResolveMapValueDescriptor</c>.
/// </summary>
public sealed class TypeCheckerMapLiteralTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) { Identifiers.Add(node); return default; }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static IdentifierExpr FindIdentifier(CompilationUnit unit, string name) {
        IdentifierCollector collector = new();
        collector.Visit(unit);
        return collector.Identifiers.First(id => id.Name == name);
    }

    // -----------------------------------------------------------------------
    // MapDescriptorOf's literal tier — indexer and for-in typing from a literal,
    // proving the descriptor actually threads through (not merely that it parses).
    // -----------------------------------------------------------------------

    [Fact]
    public void IndexRead_OnMapLiteral_ResolvesToNullableInt() {
        DiagnosticBag bag = Check("""
            m := map<string, int>{"a": 1}
            n: int := m["a"]
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0104", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(11, diag.Range.Start.Column);
    }

    [Fact]
    public void IndexRead_OnMapLiteral_AssignableToNullableInt_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            m := map<string, int>{"a": 1}
            n: int? := m["a"]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void ForInMapLiteral_BindsValueAsMapValueType_NotUnknown() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            m := map<string, int>{"a": 1}
            for k, v in m {
            print(k)
            print(v)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        Assert.Equal(GrobType.Int, FindIdentifier(unit, "v").ResolvedType);
        Assert.Equal(GrobType.String, FindIdentifier(unit, "k").ResolvedType);
    }

    // -----------------------------------------------------------------------
    // Value-type mismatch — E0004
    // -----------------------------------------------------------------------

    [Fact]
    public void EntryValue_WrongScalarType_ReportsE0004() {
        DiagnosticBag bag = Check("""m := map<string, int>{"a": "x"}""");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0004", diag.Code);
    }

    [Fact]
    public void EntryValue_MatchingScalarType_NoDiagnostics() {
        DiagnosticBag bag = Check("""m := map<string, int>{"a": 1, "b": 2}""");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void EntryValue_IntLiteralForFloatValueType_WidensImplicitly_NoDiagnostics() {
        // int -> float is the one implicit widening conversion (D-178) — an int literal
        // value in a map<string, float> literal must be accepted, mirroring ordinary
        // assignment/argument widening.
        DiagnosticBag bag = Check("""m := map<string, float>{"a": 1}""");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Nested map<string, T[]> literal value — resolves and checks element identity.
    // -----------------------------------------------------------------------

    [Fact]
    public void NestedArrayValue_Resolves_ElementAccessTypesCorrectly() {
        DiagnosticBag bag = Check("""
            m := map<string, int[]>{"a": [1, 2]}
            xs: int[] := m["a"]
            """);
        // m["a"] is int[]? (nullable) — assigning to non-nullable int[] is E0104, proving
        // the nested ArrayTypeDescriptor threaded through the map-literal's value.
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0104", diag.Code);
    }

    [Fact]
    public void NestedArrayValue_AssignableToNullableArrayAnnotation_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            m := map<string, int[]>{"a": [1, 2]}
            xs: int[]? := m["a"]
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void NestedArrayValue_WrongElementType_ReportsE0004() {
        DiagnosticBag bag = Check("""m := map<string, int[]>{"a": ["x"]}""");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0004", diag.Code);
    }

    // -----------------------------------------------------------------------
    // Duplicate keys — E0016
    // -----------------------------------------------------------------------

    [Fact]
    public void DuplicateKey_ReportsE0016Once() {
        DiagnosticBag bag = Check("""m := map<string, int>{"a": 1, "a": 2}""");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0016", diag.Code);
    }

    [Fact]
    public void NoDuplicateKeys_NoDiagnostic() {
        DiagnosticBag bag = Check("""m := map<string, int>{"a": 1, "b": 2}""");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void DuplicateKey_ValueStillChecked_BothDiagnosticsReported() {
        // The duplicate-key entry's own value is still type-checked (not skipped) — both
        // E0016 (the duplicate) and E0004 (the second entry's bad value type) are reported.
        DiagnosticBag bag = Check("""m := map<string, int>{"a": 1, "a": "x"}""");
        Assert.Equal(2, bag.Errors.Count());
        Assert.Contains(bag.Errors, d => d.Code == "E0016");
        Assert.Contains(bag.Errors, d => d.Code == "E0004");
    }

    [Fact]
    public void ThreeDuplicateKeys_ReportsE0016TwiceForSecondAndThirdOccurrence() {
        DiagnosticBag bag = Check("""m := map<string, int>{"a": 1, "a": 2, "a": 3}""");
        Assert.Equal(2, bag.Errors.Count(d => d.Code == "E0016"));
    }

    // -----------------------------------------------------------------------
    // Malformed map<X> literal (fewer than two type arguments) — permissive, no crash.
    // -----------------------------------------------------------------------

    [Fact]
    public void SingleTypeArgument_NoDescriptor_StaysPermissive() {
        DiagnosticBag bag = Check("""m := map<string>{"a": 1}""");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }
}
