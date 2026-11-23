using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for the minimal variadic native support added in Sprint 8
/// Increment B (now a variadic <c>SignatureParameter</c>, D-426), whose only
/// consumer is <c>path.join(parts: string...)</c>. Every other native keeps the
/// pre-existing fixed-arity path (<see cref="TypeCheckerNamespaceAccessTests"/>) — this
/// file covers only the additive variadic branch in <c>CheckNativeCall</c>.
/// </summary>
public sealed class TypeCheckerVariadicNativeTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        return bag;
    }

    [Fact]
    public void PathJoin_OneArgument_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly x := path.join("C:\\Reports")""");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void PathJoin_ManyArguments_NoDiagnostics() {
        DiagnosticBag bag = Check(
            """readonly x := path.join("C:\\Reports", "2026", "Q1", "summary.xlsx")""");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void PathJoin_ZeroArguments_ReportsSingleE0003NamingTheVariadicParameter() {
        // The tail's minimum of one (gate report F9) is the binder's "no default": the
        // missing parameter is named, as on every binding path (D-452 Decision 1).
        DiagnosticBag bag = Check("readonly x := path.join()");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal("'path.join' expects an argument for required parameter 'parts', but none was supplied.", diag.Message);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void PathJoin_WrongTypedTailArgument_ReportsSingleE0004_AtThatArgument() {
        DiagnosticBag bag = Check("""readonly x := path.join("C:\\Reports", 2026)""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Contains("Argument 2", diag.Message);
    }

    [Fact]
    public void PathJoin_TailArgumentAlreadyErrored_SuppressesCascade() {
        // The bad-argument sub-expression itself already reported a diagnostic;
        // path.join must not add a second one for the same slot (cascade suppression).
        DiagnosticBag bag = Check("""readonly x := path.join("a", undefinedVar)""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1001.Code, diag.Code);
    }
}
