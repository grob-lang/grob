using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// A native whose signature returns <c>T[]</c> of a named type keeps the element's name
/// (D-440), so an element read by <c>for...in</c> or by index resolves its members like
/// any other value of that type. Before D-440 the namespace-native and named-type-method
/// paths kept only the outer <c>array</c>, and the primitive-member path dropped the
/// element's name, so <c>m.nope</c> on a <c>Match</c> element passed unchecked.
/// </summary>
public sealed class TypeCheckerNativeArrayElementTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        return bag;
    }

    private static string FormatErrors(DiagnosticBag bag) =>
        string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"));

    [Fact]
    public void NamespaceNative_ForInElement_ResolvesItsMembers() {
        DiagnosticBag bag = Check("""
            for m in regex.matchAll("a", "aa") {
                v: string := m.value
                g: string? := m.group("x")
            }
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void NamespaceNative_ForInElementUnknownMember_ReportsE1002() {
        DiagnosticBag bag = Check("""
            for m in regex.matchAll("a", "aa") {
                print(m.nope)
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Contains("'Match'", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(11, diag.Range.Start.Column);
    }

    [Fact]
    public void NamespaceNative_IndexedElementMember_IsTyped() {
        DiagnosticBag bag = Check("""
            ms := regex.matchAll("a", "aa")
            n: int := ms[0].value
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Contains("type 'string'", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(11, diag.Range.Start.Column);
    }

    [Fact]
    public void NamedTypeMethod_ForInElementUnknownMember_ReportsE1002() {
        DiagnosticBag bag = Check("""
            r := regex.compile("a")
            for m in r.matchAll("aa") {
                print(m.nope)
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(11, diag.Range.Start.Column);
    }

    [Fact]
    public void NamedTypeMethod_AnnotatedBinding_AcceptsTheArray() {
        DiagnosticBag bag = Check("""
            r := regex.compile("a")
            ms: Match[] := r.matchAll("aa")
            for m in ms {
                print(m.index)
            }
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void NamedTypeArrayProperty_IndexedElement_IsTyped() {
        DiagnosticBag bag = Check("""
            for m in regex.matchAll("a", "aa") {
                n: int := m.groups[0]
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Contains("type 'string'", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void UserStructArray_IndexedElementMember_IsTyped() {
        DiagnosticBag bag = Check("""
            type Point {
                x: int
            }
            ps: Point[] := [Point { x: 1 }]
            n: string := ps[0].x
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Contains("type 'int'", diag.Message);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(14, diag.Range.Start.Column);
    }

    [Fact]
    public void UserStructArray_IndexedElementUnknownMember_ReportsE1002() {
        DiagnosticBag bag = Check("""
            type Point {
                x: int
            }
            ps: Point[] := [Point { x: 1 }]
            print(ps[0].nope)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void BindingFromIndexedElement_KeepsTheNamedType() {
        DiagnosticBag bag = Check("""
            ms := regex.matchAll("a", "aa")
            m := ms[0]
            print(m.nope)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Contains("'Match'", diag.Message);
        Assert.Equal(3, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void StringArrayResult_ElementStaysAString() {
        DiagnosticBag bag = Check("""
            for part in regex.split(",", "a,b") {
                n: int := part
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Contains("type 'string'", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }
}
