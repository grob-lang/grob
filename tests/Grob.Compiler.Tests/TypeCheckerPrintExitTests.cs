using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for <c>print</c>/<c>exit</c> call validation (D-381). Unlike
/// <c>input()</c> (<see cref="TypeCheckerInputTests"/>), neither builtin has any runtime
/// representation outside the dedicated <c>Print</c>/<c>Exit</c> opcodes
/// <c>Compiler.Statements.cs</c>'s <c>VisitExpressionStmt</c> emits for the exact
/// bare-statement-call shape it recognises structurally. Before this fix the type checker
/// treated every other shape — wrong arity even in statement position, or any call/bare
/// reference used where a value is expected — as permissively <see cref="GrobType.Unknown"/>,
/// so the compiler fell through to a generic global lookup for a global that was never
/// defined, crashing the VM at runtime with a confusing "Undefined global" fault instead of
/// a compile error. Reuses <see cref="ErrorCatalog.E0003"/> (arity, mirroring
/// <see cref="TypeCheckerInputTests"/>'s shape for the other no-namespace-native builtin)
/// and <see cref="ErrorCatalog.E1004"/> (value-position use — the same code
/// <c>VisitIdentifier</c>'s namespace-as-value arm already uses for the identical shape of
/// mistake: a compile-time-only construct referenced where a value is expected).
/// </summary>
public sealed class TypeCheckerPrintExitTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    private static string FormatErrors(DiagnosticBag bag) =>
        string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"));

    // -----------------------------------------------------------------------
    // Valid bare-statement shape — unaffected, must keep compiling.
    // -----------------------------------------------------------------------

    [Fact]
    public void Print_OneArgument_BareStatement_NoDiagnostics() {
        DiagnosticBag bag = Check("""print("hello")""");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    [Fact]
    public void Exit_NoArguments_BareStatement_NoDiagnostics() {
        DiagnosticBag bag = Check("exit()");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    [Fact]
    public void Exit_OneIntArgument_BareStatement_NoDiagnostics() {
        DiagnosticBag bag = Check("exit(1)");
        Assert.False(bag.HasErrors, FormatErrors(bag));
    }

    // -----------------------------------------------------------------------
    // Wrong arity, even in the otherwise-valid bare-statement position.
    // -----------------------------------------------------------------------

    [Fact]
    public void Print_ZeroArguments_ReportsSingleE0003() {
        DiagnosticBag bag = Check("print()");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void Print_TwoArguments_ReportsSingleE0003() {
        DiagnosticBag bag = Check("""print("a", "b")""");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
    }

    [Fact]
    public void Exit_TwoArguments_ReportsSingleE0003() {
        DiagnosticBag bag = Check("exit(1, 2)");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
    }

    // -----------------------------------------------------------------------
    // Used where a value is expected — the crash this fix eliminates.
    // -----------------------------------------------------------------------

    [Fact]
    public void Print_CallAssignedToVariable_ReportsSingleE1004() {
        DiagnosticBag bag = Check("""y := print("x")""");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    [Fact]
    public void Exit_CallAssignedToVariable_ReportsSingleE1004() {
        DiagnosticBag bag = Check("y := exit()");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
    }

    [Fact]
    public void Print_CallAsArithmeticOperand_ReportsSingleE1004_NoCascade() {
        // Cascade suppression (VisitBinary's Error pass-through) means the outer '+'
        // does not also raise a second, derived E0002 — only the root cause is reported.
        DiagnosticBag bag = Check("""x := 1 + print("y")""");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
    }

    [Fact]
    public void Print_BareIdentifierReference_ReportsSingleE1004() {
        DiagnosticBag bag = Check("y := print");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    [Fact]
    public void Exit_BareIdentifierReference_ReportsSingleE1004() {
        DiagnosticBag bag = Check("y := exit");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
    }
}
