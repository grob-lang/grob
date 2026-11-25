using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Compiler.Ast.Expressions;
using Grob.Core;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 10: adds an argument to a call, removes one or names one (R-57, E0003, E0004). A named
/// argument takes its name from the parameters of the function the call names, when the program
/// declares it, and so can name the wrong parameter as well as the right one.
/// </summary>
internal sealed class EditArgument : IMutationOperator {
    private const string AddedArgument = "1";
    private const string FallbackName = "x";

    /// <inheritdoc/>
    public string Name => "edit-argument";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        Dictionary<string, FnDecl> functions = context.Of<FnDecl>()
            .GroupBy(f => f.Node.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Node, StringComparer.Ordinal);

        var candidates = new List<IReadOnlyList<SourceEdit>>();
        foreach ((CallExpr call, _) in context.Of<CallExpr>()) {
            if (call.Arguments.Count == 0) {
                AddToEmptyCall(context, call, candidates);
                continue;
            }

            IReadOnlyList<(int First, int Last)> spans = ArgumentTokens(context, call);
            if (spans.Count != call.Arguments.Count) continue;

            AddAfterLast(context, spans[^1].Last, candidates);
            for (int i = 0; i < spans.Count; i++) {
                candidates.Add(Remove(context.Tokens, spans, i));
                foreach (string name in NamesFor(call, i, functions)) {
                    candidates.Add([SourceEdit.Insert(context.Tokens.Start(spans[i].First), $"{name}: ")]);
                }
            }
        }
        return candidates;
    }

    private static IReadOnlyList<(int First, int Last)> ArgumentTokens(MutationContext context, CallExpr call) {
        var spans = new List<(int, int)>();
        foreach (CallArgument argument in call.Arguments) {
            int first = context.FirstToken(argument);
            int last = first < 0 ? -1 : context.Tokens.ExpressionLast(first);
            if (last >= first) spans.Add((first, last));
        }
        return spans;
    }

    private static void AddAfterLast(MutationContext context, int lastToken, List<IReadOnlyList<SourceEdit>> candidates) =>
        candidates.Add([SourceEdit.Insert(context.Tokens.End(lastToken), $", {AddedArgument}")]);

    // A call with no arguments. The parser ends a call at the start of its last token, the closing
    // parenthesis, so that token is the one at the end of the call's range. MutationRangeTests
    // fails if a parser change moves the end.
    private static void AddToEmptyCall(MutationContext context, CallExpr call, List<IReadOnlyList<SourceEdit>> candidates) {
        int close = context.Tokens.IndexAt(context.Tokens.OffsetOf(call.Range.End));
        if (close > 0 && context.Tokens[close].Kind == TokenKind.RightParen && context.Tokens[close - 1].Kind == TokenKind.LeftParen) {
            candidates.Add([SourceEdit.Insert(context.Tokens.Start(close), AddedArgument)]);
        }
    }

    // The comma goes with the argument: the one after it, or for the last argument the one before.
    private static IReadOnlyList<SourceEdit> Remove(TokenSpans tokens, IReadOnlyList<(int First, int Last)> spans, int index) {
        (int first, int last) = spans[index];
        if (index < spans.Count - 1) return [SourceEdit.Delete(tokens.Start(first), tokens.Start(spans[index + 1].First))];
        if (index > 0) return [SourceEdit.Delete(tokens.End(spans[index - 1].Last), tokens.End(last))];
        return [SourceEdit.Delete(tokens.Start(first), tokens.End(last))];
    }

    private static IEnumerable<string> NamesFor(CallExpr call, int index, Dictionary<string, FnDecl> functions) {
        if (call.Arguments[index].Name is not null) return [];
        if (call.Callee is IdentifierExpr callee && functions.TryGetValue(callee.Name, out FnDecl? fn) && fn.Parameters.Count > 0) {
            return fn.Parameters.Select(p => p.Name).Distinct(StringComparer.Ordinal).Take(2);
        }
        return [FallbackName];
    }
}
