using Grob.Compiler.Ast.Expressions;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 5: swaps a binary operator's operands, so the operand whose type the checker settles
/// first is the other one (R-61: a binary operator with one operand of unknown type).
/// </summary>
internal sealed class SwapOperands : IMutationOperator {
    /// <inheritdoc/>
    public string Name => "swap-operands";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach ((BinaryExpr binary, _) in context.Of<BinaryExpr>()) {
            if (BinaryParts.Of(context, binary) is { } parts) {
                candidates.Add([SourceEdit.Replace(parts.Start, parts.End, $"{parts.Right} {parts.Operator} {parts.Left}")]);
            }
        }
        return candidates;
    }
}
