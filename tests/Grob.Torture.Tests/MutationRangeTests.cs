using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Grob.Torture.Mutation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The mutator's own range-fidelity tests (gate report §1.6). An operator takes a node's start from
/// the tree and its extent from the tokens, because the parser's <c>Range.End</c> is not the end of
/// a node (R-74). These tests fail if a parser change moves what the mutator does rely on: that
/// every node starts at a token, and that a call with no arguments ends at its closing parenthesis.
/// </summary>
public sealed class MutationRangeTests {
    private static readonly IReadOnlyList<MutationContext> Contexts = LoadContexts();

    private static List<MutationContext> LoadContexts() {
        string root = SeedCorpus.LocateRepositoryRoot(AppContext.BaseDirectory);
        return [.. SeedCorpus.Load(root).Select(s => MutationContext.TryCreate(s.Source)).OfType<MutationContext>()];
    }

    [Fact]
    public void Corpus_EverySeed_HasATreeToMutate() {
        Assert.Equal(SeedCorpus.Load(SeedCorpus.LocateRepositoryRoot(AppContext.BaseDirectory)).Count, Contexts.Count);
    }

    [Fact]
    public void Corpus_EveryNode_StartsAtAToken() {
        var misplaced = new List<string>();
        foreach (MutationContext context in Contexts) {
            foreach (AstEntry entry in context.Nodes) {
                if (AstNodes.RangeOf(entry.Node) is { } range && context.Tokens.IndexAt(range.Start) < 0) {
                    misplaced.Add($"{entry.Node.GetType().Name} at {range.Start}");
                }
            }
        }

        Assert.True(misplaced.Count == 0, "nodes whose start is not a token start:\n  " + string.Join("\n  ", misplaced.Take(20)));
    }

    [Fact]
    public void Corpus_EveryCallWithNoArguments_EndsAtItsClosingParenthesis() {
        int checkedCalls = 0;
        var misplaced = new List<string>();
        foreach (MutationContext context in Contexts) {
            foreach ((CallExpr call, _) in context.Of<CallExpr>().Where(c => c.Node.Arguments.Count == 0)) {
                checkedCalls++;
                int close = context.Tokens.IndexAt(context.Tokens.OffsetOf(call.Range.End));
                if (close < 1 || context.Tokens[close].Kind != TokenKind.RightParen || context.Tokens[close - 1].Kind != TokenKind.LeftParen) {
                    misplaced.Add($"call at {call.Range.Start}");
                }
            }
        }

        Assert.True(checkedCalls > 100, "the corpus has too few calls with no arguments to trust this check");
        Assert.True(misplaced.Count == 0, "calls that do not end at their parenthesis:\n  " + string.Join("\n  ", misplaced.Take(20)));
    }

    [Theory]
    [InlineData("x := 1 + 2\n", 1, 6, 1, 10)]
    [InlineData("g()\n", 1, 1, 1, 3)]
    public void Range_BinaryAndCall_EndAtTheStartOfTheirLastToken(string source, int startLine, int startColumn, int endLine, int endColumn) {
        // R-74 as measured at HEAD, not as it was first written: neither a binary expression nor a
        // call ends at the end of its last token. When the parser is fixed this test fails, and the
        // mutator's reliance on the token stream can be reviewed.
        MutationContext context = MutationContext.TryCreate(source)!;
        SourceRange range = AstNodes.RangeOf(context.Nodes.First(e => e.Node is BinaryExpr or CallExpr).Node)!.Value;

        Assert.Equal((startLine, startColumn, endLine, endColumn), (range.Start.Line, range.Start.Column, range.End.Line, range.End.Column));
    }

    // An expression that a statement's block follows ends before the block: a loop's iterable has
    // no closing bracket to stop at, and a condition's closing parenthesis must still stop it.
    [Theory]
    [InlineData("for x in a + b {\n    g()\n}\n", "a + b")]
    [InlineData("for i in 0..n {\n    g()\n}\n", "0..n")]
    [InlineData("if (a == b) {\n    g()\n}\n", "a == b")]
    [InlineData("while (a < b) {\n    g()\n}\n", "a < b")]
    public void ExpressionSpan_ExpressionBeforeAStatementBlock_EndsBeforeTheBlock(string source, string expected) {
        MutationContext context = MutationContext.TryCreate(source)!;
        Expression expression = context.Nodes.Select(e => e.Node switch {
            ForInStmt f => f.Iterable,
            IfStmt i => i.Condition,
            WhileStmt w => w.Condition,
            _ => null,
        }).OfType<Expression>().First();

        (int start, int end) = context.ExpressionSpan(expression)!.Value;

        Assert.Equal(expected, context.Source[start..end]);
    }
}
