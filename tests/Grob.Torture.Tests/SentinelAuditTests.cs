using Xunit;

using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Core;

namespace Grob.Torture.Tests;

/// <summary>
/// The §3.1.1 invariant, as the oracle can check it (gate report §1.1, corrections): after
/// checking, no identifier has a null <c>Declaration</c>, and <c>Declaration</c> is
/// <c>UnresolvedDecl.Instance</c> exactly when <c>ResolvedType</c> is <c>Error</c>.
/// </summary>
public sealed class SentinelAuditTests {
    private static CompilationUnit ParseOnly(string source) {
        var bag = new DiagnosticBag();
        return Parser.Parse(Lexer.Scan(source, bag, "program.grob"), bag);
    }

    [Fact]
    public void Audit_IdentifierNeverVisited_IsSentinelFindingWithItsRole() {
        CompilationUnit unit = ParseOnly("x := 1\nprint(x)\n");

        IReadOnlyList<Finding> findings = SentinelAudit.Audit(unit, ["E0002", "E0001"]);

        Assert.Equal(
            [
                new Signature(PipelinePhase.Check, FindingClass.Sentinel,
                    "no-declaration|ExpressionStmt.Expression/CallExpr.Callee|E0001,E0002"),
                new Signature(PipelinePhase.Check, FindingClass.Sentinel,
                    "no-declaration|CallExpr.Arguments/CallArgument.Value|E0001,E0002"),
            ],
            findings.Select(f => f.Signature));
    }

    [Fact]
    public void Audit_TwoUnvisitedIdentifiersInOneRole_IsOneFindingPerRole() {
        CompilationUnit unit = ParseOnly("print(a)\nprint(b)\n");

        Assert.Equal(2, SentinelAudit.Audit(unit, []).Count);
    }

    [Fact]
    public void Audit_UnresolvedDeclarationWithAResolvedType_IsDisagreeFinding() {
        CompilationUnit unit = ParseOnly("print(x)\n");
        IdentifierExpr x = Identifiers(unit).Single(i => i.Name == "x");
        foreach (IdentifierExpr id in Identifiers(unit)) {
            id.Declaration = UnresolvedDecl.Instance;
            id.ResolvedType = GrobType.Error;
        }
        x.ResolvedType = GrobType.Int;

        Finding finding = Assert.Single(SentinelAudit.Audit(unit, []));

        Assert.StartsWith("disagree|", finding.Signature.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Audit_ErrorTypeWithARealDeclaration_IsCascadeSuppressionNotAFinding() {
        CompilationUnit unit = ParseOnly("print(x)\n");
        foreach (IdentifierExpr id in Identifiers(unit)) {
            id.Declaration = UnresolvedDecl.Instance;
            id.ResolvedType = GrobType.Error;
        }
        Identifiers(unit).Single(i => i.Name == "x").Declaration = unit;

        Assert.Empty(SentinelAudit.Audit(unit, []));
    }

    [Fact]
    public void Run_UseOfABindingWithAnErrorType_IsNotAFinding() {
        OracleResult result = new Oracle(OracleBudget.Ci).Run("x: int := \"a\"\nprint(x)\n");

        Assert.Equal(OutcomeKind.Diagnostics, result.Outcome);
    }

    [Theory]
    [InlineData("fn f(): int {\n    return 1 +\n}\n")]
    [InlineData("try { x := 1 } finally { y := 2 } catch (e: IoError) { z := 3 }\n")]
    [InlineData("fn (\n")]
    public void Audit_ErrorNodes_AreSkipped(string source) {
        CompilationUnit unit = ParseOnly(source);

        Assert.All(SentinelAudit.Audit(unit, []), f => Assert.DoesNotContain("Error", f.Signature.Detail, StringComparison.Ordinal));
    }

    [Fact]
    public void RoleOf_NodeNotInTheTree_IsUnknown() {
        CompilationUnit unit = ParseOnly("print(1)\n");
        var stranger = new IdentifierExpr(unit.Range, "stranger");

        Assert.Equal(AstPath.Unknown, AstPath.RoleOf(unit, stranger));
    }

    [Fact]
    public void Signature_ToString_IsItsKey() {
        var signature = new Signature(PipelinePhase.Lex, FindingClass.Location, "E0001|end");

        Assert.Equal("Lex|Location|E0001|end", signature.ToString());
    }

    [Fact]
    public void Audit_CheckedProgram_FindsNothing() {
        Assert.Empty(new Oracle(OracleBudget.Ci).Run("x := 1\nfn f(a: int): int {\n    return a + x\n}\nprint(f(2))\n").Findings);
    }

    [Fact]
    public void Run_F4OptionalAssignmentTarget_IsSentinelFinding() {
        const string source = "type Config {\n    port: int\n}\nc := Config { port: 8080 }\nc?.port = 9090\n";

        OracleResult result = new Oracle(OracleBudget.Ci).Run(source);

        Assert.Equal(OutcomeKind.Finding, result.Outcome);
        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(FindingClass.Sentinel, finding.Signature.Class);
        Assert.EndsWith($"|{ErrorCatalog.E0206.Code}", finding.Signature.Detail, StringComparison.Ordinal);
    }

    private static List<IdentifierExpr> Identifiers(CompilationUnit unit) {
        var collector = new IdentifierCollector();
        collector.Visit(unit);
        return collector.Found;
    }

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Found { get; } = [];

        public override Unit VisitIdentifier(IdentifierExpr node) {
            Found.Add(node);
            return default;
        }

        public override Unit VisitErrorExpr(ErrorExpr node) => default;

        public override Unit VisitErrorStmt(Grob.Compiler.Ast.Statements.ErrorStmt node) => default;

        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }
}
