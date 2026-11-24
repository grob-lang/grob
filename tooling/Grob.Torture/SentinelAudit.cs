using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;

namespace Grob.Torture;

/// <summary>
/// The §3.1.1 invariant as the oracle can check it (D-311; gate report §1.1, corrections). After
/// checking, every identifier has a non-null <c>Declaration</c>, and an identifier whose
/// <c>Declaration</c> is <see cref="UnresolvedDecl.Instance"/> has <c>ResolvedType</c>
/// <see cref="GrobType.Error"/>. The converse does not hold: a name that resolves to a
/// declaration whose own type is <see cref="GrobType.Error"/> carries that real declaration
/// and the <see cref="GrobType.Error"/> type, which is cascade suppression (§29.3), not a
/// violation.
/// </summary>
/// <remarks>
/// <c>ResolvedType</c> cannot be checked on its own: it is an enum that defaults to
/// <see cref="GrobType.Unknown"/>, which is also a legal resolved type, so an unset field looks
/// like a set one. <c>VisitIdentifier</c> sets both fields together, so a null
/// <c>Declaration</c> stands for "never visited".
/// </remarks>
internal static class SentinelAudit {
    private const string NoDeclaration = "no-declaration";
    private const string Disagree = "disagree";

    /// <summary>
    /// One finding per distinct signature, in source order. The signature's detail is the
    /// violation, the identifier's role (its parent's and grandparent's owning properties) and
    /// the codes the run reported, sorted.
    /// </summary>
    /// <param name="unit">The checked compilation unit.</param>
    /// <param name="codes">Every diagnostic code the run reported up to and including the checker.</param>
    public static IReadOnlyList<Finding> Audit(CompilationUnit unit, IEnumerable<string> codes) {
        var walker = new ViolationWalker();
        walker.Visit(unit);
        if (walker.Violations.Count == 0) return [];

        string codeSet = string.Join(',', codes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        var findings = new List<Finding>();
        var seen = new HashSet<Signature>();
        foreach ((IdentifierExpr identifier, string kind) in walker.Violations) {
            var signature = new Signature(PipelinePhase.Check, FindingClass.Sentinel,
                $"{kind}|{AstPath.RoleOf(unit, identifier)}|{codeSet}");
            if (seen.Add(signature)) {
                findings.Add(new Finding(signature,
                    $"identifier '{identifier.Name}' at {identifier.Range.Start}: {kind} (ResolvedType {identifier.ResolvedType})"));
            }
        }
        return findings;
    }

    private sealed class ViolationWalker : AstWalker {
        public List<(IdentifierExpr Identifier, string Kind)> Violations { get; } = [];

        public override Unit VisitIdentifier(IdentifierExpr node) {
            if (node.Declaration is null) {
                Violations.Add((node, NoDeclaration));
            } else if (ReferenceEquals(node.Declaration, UnresolvedDecl.Instance) && node.ResolvedType != GrobType.Error) {
                Violations.Add((node, Disagree));
            }
            return default;
        }

        // Error nodes carry no identifiers to check.
        public override Unit VisitErrorExpr(ErrorExpr node) => default;

        public override Unit VisitErrorStmt(ErrorStmt node) => default;

        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }
}
