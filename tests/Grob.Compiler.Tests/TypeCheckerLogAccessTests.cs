using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 8 Increment C's <c>log</c> namespace registration in
/// <c>NamespaceRegistry</c> — arity/type validation against the compile-time twin, mirroring
/// how <c>math</c>/<c>path</c>/<c>env</c> namespace calls are already checked
/// (<see cref="TypeCheckerNamespaceAccessTests"/>, <see cref="TypeCheckerEnvAccessTests"/>).
/// </summary>
public sealed class TypeCheckerLogAccessTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    [Theory]
    [InlineData("debug")]
    [InlineData("info")]
    [InlineData("warning")]
    [InlineData("error")]
    public void LogLevelCall_ValidStringArg_NoDiagnostics(string member) {
        DiagnosticBag bag = Check($"""log.{member}("a message")""");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void LogInfo_WrongArgType_ReportsSingleE0004() {
        DiagnosticBag bag = Check("log.info(42)");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
    }

    [Fact]
    public void LogInfo_TooManyArguments_ReportsSingleE0003() {
        DiagnosticBag bag = Check("""log.info("a", "b")""");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
    }

    [Fact]
    public void LogSetLevel_ValidStringArg_NoDiagnostics() {
        DiagnosticBag bag = Check("""log.setLevel("debug")""");

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void LogUnknownMember_ReportsSingleE1003() {
        DiagnosticBag bag = Check("readonly x := log.nope()");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1003.Code, diag.Code);
    }
}
