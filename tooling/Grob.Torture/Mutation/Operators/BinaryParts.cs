using Grob.Compiler.Ast.Expressions;

namespace Grob.Torture.Mutation.Operators;

/// <summary>A binary expression cut into the source text of its two operands and its operator.</summary>
/// <param name="Start">The offset of the expression's first character.</param>
/// <param name="End">The offset after its last character.</param>
/// <param name="Left">The left operand's text.</param>
/// <param name="Operator">The operator's text.</param>
/// <param name="Right">The right operand's text.</param>
internal sealed record BinaryParts(int Start, int End, string Left, string Operator, string Right) {
    /// <summary>
    /// Cuts <paramref name="binary"/> at its operator. The operator is the token before the right
    /// operand's first token, whose start the parser records exactly. The expression's end comes
    /// from the tokens, not from its <c>Range.End</c> (R-74).
    /// </summary>
    /// <returns>The parts, or <see langword="null"/> when the tree and the tokens do not agree.</returns>
    public static BinaryParts? Of(MutationContext context, BinaryExpr binary) {
        TokenSpans tokens = context.Tokens;
        int rightFirst = context.FirstToken(binary.Right);
        if (context.ExpressionTokens(binary) is not var (first, last) || rightFirst <= first + 1 || last < rightFirst) return null;

        int op = rightFirst - 1;
        string left = tokens.Text(tokens.Start(first), tokens.Start(op)).TrimEnd();
        string right = tokens.Text(tokens.Start(rightFirst), tokens.End(last));
        return new BinaryParts(tokens.Start(first), tokens.End(last), left, tokens.Text(tokens.Start(op), tokens.End(op)), right);
    }
}
