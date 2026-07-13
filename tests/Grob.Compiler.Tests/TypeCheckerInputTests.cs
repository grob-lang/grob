using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 8 Increment C's <c>input()</c> arity/type validation —
/// the one no-namespace native the checker validates ahead of the permissive
/// <c>print</c>/<c>exit</c> built-in fallback in <c>TypeChecker.Expressions.cs VisitCall</c>.
/// Reuses the existing native-call diagnostic codes (E0003 arity, E0004 argument type)
/// rather than allocating new ones for a single call site.
/// </summary>
public sealed class TypeCheckerInputTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    [Fact]
    public void Input_ZeroArguments_ResolvesToString_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly x := input()");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Input_OneStringArgument_ResolvesToString_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly x := input("Name: ")""");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Input_ResultType_IsString() {
        DiagnosticBag bag = Check("""
            x := input()
            readonly y: string := x
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Input_WrongArgType_ReportsSingleE0004() {
        DiagnosticBag bag = Check("readonly x := input(42)");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
    }

    [Fact]
    public void Input_TooManyArguments_ReportsSingleE0003() {
        DiagnosticBag bag = Check("""readonly x := input("a", "b")""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
    }
}
