using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 8 Increment C's <c>env</c> namespace registration in
/// <c>NamespaceRegistry</c> — arity/type validation against the compile-time twin, mirroring
/// how <c>math</c>/<c>path</c> namespace calls are already checked
/// (<see cref="TypeCheckerNamespaceAccessTests"/>).
/// </summary>
public sealed class TypeCheckerEnvAccessTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    [Fact]
    public void EnvGet_ValidStringArg_ResolvesToNullableString_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly x: string? := env.get("HOME")""");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void EnvGet_WrongArgType_ReportsSingleE0004() {
        DiagnosticBag bag = Check("readonly x := env.get(42)");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
    }

    [Fact]
    public void EnvRequire_ValidStringArg_ResolvesToString_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly x: string := env.require("HOME")""");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void EnvHas_ValidStringArg_ResolvesToBool_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly x: bool := env.has("HOME")""");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void EnvSet_ValidArgs_NoDiagnostics() {
        DiagnosticBag bag = Check("""env.set("KEY", "VALUE")""");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void EnvSet_TooFewArguments_ReportsSingleE0003() {
        DiagnosticBag bag = Check("""env.set("KEY")""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
    }

    [Fact]
    public void EnvAll_NoArguments_ResolvesToMap_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly x := env.all()");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void EnvAll_WithArgument_ReportsSingleE0003() {
        DiagnosticBag bag = Check("""readonly x := env.all("nope")""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
    }

    [Fact]
    public void EnvUnknownMember_ReportsSingleE1003() {
        DiagnosticBag bag = Check("readonly x := env.nope()");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1003.Code, diag.Code);
    }
}
