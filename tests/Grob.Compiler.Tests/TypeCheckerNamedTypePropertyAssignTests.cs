using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// A named type's properties (D-356) are read-only: a plugin computes each one from the
/// value's hidden fields, and the VM's <c>SetProperty</c> has no field to write. Before
/// D-440, an assignment such as <c>d.year = 2020</c> passed the checker and reached the VM
/// as an internal error. Each mutating target form (<c>=</c>, compound assignment and
/// <c>++</c>/<c>--</c>) is now E0204 at the checker.
/// </summary>
public sealed class TypeCheckerNamedTypePropertyAssignTests {
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
    public void DateProperty_Assigned_ReportsE0204AtTheTarget() {
        DiagnosticBag bag = Check("""
            d := date.now()
            d.year = 2020
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Contains("'year'", diag.Message);
        Assert.Contains("'date'", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void DateProperty_CompoundAssigned_ReportsE0204AtTheStatement() {
        DiagnosticBag bag = Check("""
            d := date.now()
            d.year += 1
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void DateProperty_Incremented_ReportsE0204AtTheStatement() {
        DiagnosticBag bag = Check("""
            d := date.now()
            d.year++
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void DateProperty_AssignedThroughReadonlyBinding_ReportsOneE0204() {
        DiagnosticBag bag = Check("""
            readonly d := date.now()
            d.year = 2020
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Contains("`readonly`", diag.Message);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void DateUnknownMember_Assigned_ReportsOnlyE1002() {
        DiagnosticBag bag = Check("""
            d := date.now()
            d.nope = 1
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void UserStructField_Assigned_StaysValid() {
        DiagnosticBag bag = Check("""
            type Point {
                x: int
            }
            p := Point { x: 1 }
            p.x = 2
            p.x += 1
            p.x++
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }
}
