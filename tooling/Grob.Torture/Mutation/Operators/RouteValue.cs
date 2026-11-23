using Grob.Compiler.Ast;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 7: routes a value through an array element or an anonymous struct field and reads it
/// back, so its type becomes one the checker must recover (R-57: unknown element types):
/// <c>e</c> becomes <c>[e][0]</c> or <c>#{ v: e }.v</c>.
/// </summary>
/// <remarks>
/// The gate report's third form, a map value, is not made. A map literal names its key and value
/// types (<c>map&lt;string, T&gt;{ … }</c>), and <c>T</c> is the type the operator cannot know
/// without the checker.
/// </remarks>
internal sealed class RouteValue : IMutationOperator {
    /// <inheritdoc/>
    public string Name => "route-value";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach (Expression value in context.ValuePositions()) {
            if (context.ExpressionSpan(value) is not var (start, end)) continue;

            string text = context.Tokens.Text(start, end);
            candidates.Add([SourceEdit.Replace(start, end, $"[{text}][0]")]);
            candidates.Add([SourceEdit.Replace(start, end, $"#{{ v: {text} }}.v")]);
        }
        return candidates;
    }
}
