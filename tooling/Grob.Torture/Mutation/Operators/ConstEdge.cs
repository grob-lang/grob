using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 11: makes a constant's operand zero, or the constant the smallest integer, so the
/// compiler's folding meets a division by zero or an overflow (F-1, R-68). The checker accepts
/// both; the compiler folds a constant expression before any statement runs.
/// </summary>
internal sealed class ConstEdge : IMutationOperator {
    private const string SmallestInteger = "-9223372036854775807 - 1";

    /// <inheritdoc/>
    public string Name => "const-edge";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach ((ConstDecl constant, _) in context.Of<ConstDecl>()) {
            if (context.ExpressionSpan(constant.Value) is var (start, end)) {
                candidates.Add([SourceEdit.Replace(start, end, SmallestInteger)]);
            }
            foreach (AstEntry operand in AstNodes.Enumerate(constant.Value).Where(e => e.Node is IntLiteralExpr && e.Parent is BinaryExpr)) {
                int token = context.FirstToken(operand.Node);
                if (token >= 0) {
                    candidates.Add([SourceEdit.Replace(context.Tokens.Start(token), context.Tokens.End(token), "0")]);
                }
            }
        }
        return candidates;
    }
}
