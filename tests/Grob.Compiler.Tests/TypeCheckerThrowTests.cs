using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 7 Increment A — <c>throw</c> operand
/// type-checking (E0014). The operand must resolve to a <c>GrobError</c>
/// subtype; everything else is rejected at the operand's own range.
/// </summary>
public sealed class TypeCheckerThrowTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    // -----------------------------------------------------------------------
    // Happy path — a constructed leaf, or an already-bound GrobError value.
    // -----------------------------------------------------------------------

    [Fact]
    public void Throw_ConstructedLeaf_NoError() {
        DiagnosticBag bag = Check("""
            throw IoError { message: "x" }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Throw_BoundGrobErrorValue_NoError() {
        // The value's static type is GrobError itself — the reflexive case.
        DiagnosticBag bag = Check("""
            readonly e := GrobError { message: "x" }
            throw e
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Throw_BoundLeafValue_NoError() {
        DiagnosticBag bag = Check("""
            readonly e := IoError { message: "x" }
            throw e
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Rejection — E0014 for a non-GrobError operand.
    // -----------------------------------------------------------------------

    [Fact]
    public void Throw_IntLiteral_EmitsE0014() {
        DiagnosticBag bag = Check("throw 42\n");

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0014", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
    }

    [Fact]
    public void Throw_StringLiteral_EmitsE0014() {
        DiagnosticBag bag = Check("""
            throw "oops"
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0014", d.Code);
    }

    [Fact]
    public void Throw_BoundIntVariable_EmitsE0014() {
        DiagnosticBag bag = Check("""
            readonly n := 5
            throw n
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0014", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
    }

    [Fact]
    public void Throw_UnrelatedUserStruct_EmitsE0014() {
        DiagnosticBag bag = Check("""
            type Config {
            host: string
            }
            throw Config { host: "x" }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0014", d.Code);
    }

    // -----------------------------------------------------------------------
    // Cascade suppression — an already-erroring operand does not also emit E0014.
    // -----------------------------------------------------------------------

    [Fact]
    public void Throw_UndefinedIdentifier_OnlyE1001_NoE0014Cascade() {
        DiagnosticBag bag = Check("throw undefinedThing\n");

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E1001", d.Code);
    }

    // -----------------------------------------------------------------------
    // Construction rules still apply through 'throw' — E0103/E0012 reused.
    // -----------------------------------------------------------------------

    [Fact]
    public void Throw_MissingRequiredMessage_EmitsE0103() {
        DiagnosticBag bag = Check("throw IoError { }\n");

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0103", d.Code);
    }

    [Fact]
    public void Throw_UnknownField_EmitsE0012() {
        DiagnosticBag bag = Check("""
            throw IoError { message: "x", typo: 1 }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0012", d.Code);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant — every identifier node has ResolvedType + Declaration.
    // -----------------------------------------------------------------------

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) { Identifiers.Add(node); return default; }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    [Fact]
    public void Throw_IdentifierNodes_HaveNonNullResolvedTypeAndDeclaration() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            readonly msg := "boom"
            throw IoError { message: msg }
            """);

        IdentifierCollector collector = new();
        collector.Visit(unit);

        foreach (IdentifierExpr id in collector.Identifiers) {
            Assert.True(id.ResolvedType != GrobType.Unknown || bag.HasErrors,
                $"Identifier '{id.Name}' at {id.Range} has UnknownType after type-check.");
            Assert.NotSame(UnresolvedDecl.Instance, id.Declaration);
            Assert.NotNull(id.Declaration);
        }
    }

    // -----------------------------------------------------------------------
    // Layer-invariant — pathological but parseable throw operands never throw.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("throw 1\n")]
    [InlineData("throw nil\n")]
    [InlineData("throw true\n")]
    [InlineData("throw undefinedThing\n")]
    [InlineData("throw IoError { }\n")]
    [InlineData("throw IoError { message: 1 }\n")]
    [InlineData("type T { }\nthrow T { }\n")]
    public void Throw_PathologicalOperands_NeverThrows(string source) {
        Exception? thrown = Record.Exception(() => Check(source));
        Assert.Null(thrown);
    }
}
