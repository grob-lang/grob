using System.IO;

using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 1 end-to-end smoke test. Runs the lexer + parser pipeline on
/// representative <c>.grob</c> sources and asserts:
///   * neither stage throws,
///   * a <see cref="CompilationUnit"/> is produced,
///   * the diagnostic count matches the fixture's documented expectation.
///
/// This is intentionally not an execution test — there is no VM yet.
/// The intent is to prove the lexer/parser pipeline boundary holds at the
/// integration level (assemblies wired correctly, fixtures loadable,
/// public API surface usable from outside the compiler test project).
/// </summary>
public class PipelineSmokeTests {
    private static string FixturePath(string name) {
        if (Path.IsPathRooted(name))
            throw new ArgumentException($"Fixture name must be a relative path, got: {name}", nameof(name));
        return Path.Combine(AppContext.BaseDirectory, "fixtures", "sprint-1", name);
    }

    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) LexAndParse(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        return (unit, bag);
    }

    [Fact]
    public void RepresentativeValidProgram_LexesAndParsesWithNoDiagnostics() {
        string source = File.ReadAllText(FixturePath("smoke-pipeline.grob"));

        (CompilationUnit unit, DiagnosticBag bag) = LexAndParse(source);

        Assert.Equal(0, bag.Count);
        Assert.NotNull(unit);
        Assert.NotEmpty(unit.TopLevel);
    }

    [Fact]
    public void Section29_6Fixture_ProducesExactlyOneParserDiagnostic() {
        // The §29.6 worked example carries one parser-level diagnostic in
        // Sprint 1 (the second arrives with the Sprint 2 type checker).
        string source = File.ReadAllText(FixturePath("section-29-6-worked-example.grob"));

        (CompilationUnit unit, DiagnosticBag bag) = LexAndParse(source);

        Assert.Equal(1, bag.Count);
        Assert.Equal("E2001", bag.Diagnostics[0].Code);
        Assert.Equal(11, bag.Diagnostics[0].Range.Start.Line);
        Assert.Equal(1, bag.Diagnostics[0].Range.Start.Column);
        Assert.Equal(3, unit.TopLevel.Count);
    }
}
