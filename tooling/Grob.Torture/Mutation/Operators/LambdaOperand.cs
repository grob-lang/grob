using Grob.Compiler.Ast.Expressions;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 4: moves an operand into an inferred lambda's parameter: <c>a + b</c> becomes
/// <c>((mv) =&gt; mv + b)(a)</c>. The parameter has no annotation, so the binary operator sees an
/// operand of unknown type (R-61, R-63).
/// </summary>
internal sealed class LambdaOperand : IMutationOperator {
    /// <inheritdoc/>
    public string Name => "lambda-operand";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach ((BinaryExpr binary, _) in context.Of<BinaryExpr>()) {
            if (BinaryParts.Of(context, binary) is { } parts) {
                candidates.Add([SourceEdit.Replace(parts.Start, parts.End,
                    $"((mv) => mv {parts.Operator} {parts.Right})({parts.Left})")]);
            }
        }
        return candidates;
    }
}
