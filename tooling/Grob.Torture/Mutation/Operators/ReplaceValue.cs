using Grob.Compiler.Ast;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 2: replaces a value, a literal or any expression, with a literal of another type or
/// <c>nil</c>. The expression form is what reached R-58 (<c>rabs: float := f.abs()</c> becomes
/// <c>rabs: float := 1</c>, closed by D-460) and reaches R-60 (<c>xs.filter(x =&gt; …)</c> becomes
/// <c>xs.filter(5)</c>).
/// </summary>
internal sealed class ReplaceValue : IMutationOperator {
    private static readonly string[] Replacements = ["1", "1.5", "\"s\"", "true", "nil"];

    /// <inheritdoc/>
    public string Name => "replace-value";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach (Expression value in context.ValuePositions()) {
            if (context.ExpressionSpan(value) is not var (start, end)) continue;

            string current = context.Tokens.Text(start, end);
            foreach (string replacement in Replacements.Where(r => r != current)) {
                candidates.Add([SourceEdit.Replace(start, end, replacement)]);
            }
        }
        return candidates;
    }
}
