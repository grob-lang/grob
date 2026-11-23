using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Statements;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 9: deletes a statement, a <c>return</c> included, or a top-level declaration: a missing
/// return is E0005's all-paths question, and a missing declaration leaves references the checker
/// must report once and not cascade.
/// </summary>
internal sealed class DeleteStatement : IMutationOperator {
    /// <inheritdoc/>
    public string Name => "delete-statement";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach (AstEntry entry in context.Nodes) {
            if (entry.Node is not (Statement or Declaration) || entry.Node is ErrorStmt or ErrorDecl) continue;
            if (entry.Parent is not (BlockStmt or CompilationUnit)) continue;

            int first = context.FirstToken(entry.Node);
            int last = first < 0 ? -1 : context.Tokens.ExpressionLast(first);
            if (last >= first) {
                candidates.Add([SourceEdit.Delete(context.Tokens.Start(first), context.Tokens.End(last))]);
            }
        }
        return candidates;
    }
}
