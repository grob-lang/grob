using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 5: swaps a binary operator's operands, so the operand whose type the checker settles
/// first is the other one (R-61: a binary operator with one operand of unknown type). An operand
/// that is itself a binary expression is parenthesised, so the grouping is kept.
/// </summary>
internal sealed class SwapOperands : IMutationOperator {
    /// <inheritdoc/>
    public string Name => "swap-operands";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach ((BinaryExpr binary, _) in context.Of<BinaryExpr>()) {
            if (BinaryParts.Of(context, binary) is { } parts) {
                string left = Grouped(binary.Left, parts.Left);
                string right = Grouped(binary.Right, parts.Right);
                candidates.Add([SourceEdit.Replace(parts.Start, parts.End, $"{right} {parts.Operator} {left}")]);
            }
        }
        return candidates;
    }

    // A binary operand is parenthesised when it moves, so the swap changes the operands' order
    // and not how the expression groups: 1 - 2 - 3 becomes 3 - (1 - 2), not 3 - 1 - 2.
    private static string Grouped(Expression operand, string text) => operand is BinaryExpr ? $"({text})" : text;
}
