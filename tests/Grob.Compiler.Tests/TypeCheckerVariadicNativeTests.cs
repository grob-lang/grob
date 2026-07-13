using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for the minimal variadic native support added in Sprint 8
/// Increment B (<c>NamespaceRegistry.NativeMember.VariadicElementType</c>), whose only
/// consumer is <c>path.join(parts: string...)</c>. Every other native keeps the
/// pre-existing fixed-arity path (<see cref="TypeCheckerNamespaceAccessTests"/>) — this
/// file covers only the additive variadic branch in <c>CheckNativeCall</c>.
/// </summary>
public sealed class TypeCheckerVariadicNativeTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
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
    public void PathJoin_ZeroArguments_ReportsSingleE0003_AtLeastOneWording() {
        DiagnosticBag bag = Check("readonly x := path.join()");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Contains("at least 1", diag.Message);
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
