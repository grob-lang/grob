using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;

namespace Grob.Torture.Mutation.Operators;

/// <summary>
/// Operator 8: swaps <c>?.</c> and <c>.</c>, or removes a nil-check guard: the <c>if (x != nil) {</c>
/// header and its closing brace, leaving the body unguarded (R-59, R-66, F-4).
/// </summary>
internal sealed class ChangeOptionalChain : IMutationOperator {
    /// <inheritdoc/>
    public string Name => "change-optional-chain";

    /// <inheritdoc/>
    public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) {
        var candidates = new List<IReadOnlyList<SourceEdit>>();
        TokenSpans tokens = context.Tokens;
        for (int i = 0; i < tokens.Count; i++) {
            if (tokens[i].Kind == TokenKind.Dot) {
                candidates.Add([SourceEdit.Replace(tokens.Start(i), tokens.End(i), "?.")]);
            } else if (tokens[i].Kind == TokenKind.QuestionDot) {
                candidates.Add([SourceEdit.Replace(tokens.Start(i), tokens.End(i), ".")]);
            }
        }

        foreach ((IfStmt guard, _) in context.Of<IfStmt>().Where(g => g.Node.Else is null && ComparesToNil(g.Node.Condition))) {
            int ifToken = context.FirstToken(guard);
            int open = context.FirstToken(guard.Then);
            int close = open < 0 ? -1 : tokens.MatchingClose(open);
            if (ifToken < 0 || close < 0 || tokens[open].Kind != TokenKind.LeftBrace) continue;

            candidates.Add([
                SourceEdit.Delete(tokens.Start(ifToken), tokens.End(open)),
                SourceEdit.Delete(tokens.Start(close), tokens.End(close)),
            ]);
        }
        return candidates;
    }

    private static bool ComparesToNil(Expression condition) =>
        condition is BinaryExpr { Operator: BinaryOperator.Equal or BinaryOperator.NotEqual } binary
        && (binary.Left is NilLiteralExpr || binary.Right is NilLiteralExpr);
}
