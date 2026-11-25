using Grob.Torture.Mutation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// A seed, an operator and a draw give one program, the same one every time (prompt §3), and an
/// operator may not raise a seed's nesting by more than two (gate report §1.6).
/// </summary>
public sealed class MutatorTests {
    // One construct for every operator to find. Built from ordinary strings, not a raw literal:
    // the seed corpus extracts raw literals from the test projects, and a seed that is this very
    // test would change the CI slice's programs whenever the test was edited.
    private static readonly string RichSource = string.Join('\n',
        "const limit := 10 / 2",
        "fn add(a: int, b: int): int {",
        "    return a + b",
        "}",
        "s := \"ab\"",
        "name: string? := nil",
        "total := add(1, 2)",
        "if (name != nil) {",
        "    print(name)",
        "}",
        "print(s.upper())",
        "n := s?.length",
        string.Empty);

    private static IMutationOperator ReplaceValue => Mutator.Operators.Single(o => o.Name == "replace-value");

    private static Seed Rich => new("rich@1", SeedKind.Fixture, "rich", RichSource);

    private sealed class FixedOperator(string name, Func<MutationContext, IReadOnlyList<SourceEdit>> edit) : IMutationOperator {
        public string Name => name;

        public IReadOnlyList<IReadOnlyList<SourceEdit>> Candidates(MutationContext context) => [edit(context)];
    }

    [Fact]
    public void Mutate_SameSeedOperatorAndDraw_GivesTheSameMutant() {
        Mutant? first = new Mutator(42).Mutate(Rich, ReplaceValue, 7);
        Mutant? second = new Mutator(42).Mutate(Rich, ReplaceValue, 7);

        Assert.NotNull(first);
        Assert.Equal(first!.Source, second!.Source);
        Assert.Equal(first.ProgramId, second.ProgramId);
    }

    [Fact]
    public void Mutate_ProgramId_IsSeedOperatorAndDraw() {
        Mutant mutant = new Mutator(1).Mutate(Rich, Mutator.Operators[0], 3)!;

        Assert.Equal("rich@1/remove-annotation/3", mutant.ProgramId);
        Assert.Equal(("rich@1", "remove-annotation", 3), (mutant.SeedId, mutant.Operator, mutant.Draw));
        Assert.Equal(Mutator.ProgramId("rich@1", "remove-annotation", 3), mutant.ProgramId);
    }

    [Fact]
    public void Mutate_DifferentDraws_ReachDifferentCandidates() {
        var mutator = new Mutator(1);
        List<string> sources = [.. Enumerable.Range(0, 40).Select(draw => mutator.Mutate(Rich, ReplaceValue, draw)?.Source).OfType<string>().Distinct()];

        Assert.True(sources.Count > 5, $"40 draws reached only {sources.Count} programs");
    }

    [Fact]
    public void Mutate_DifferentCampaignSeeds_ReachDifferentCandidates() {
        List<string> sources = [.. Enumerable.Range(1, 40).Select(seed => new Mutator((ulong)seed).Mutate(Rich, ReplaceValue, 0)?.Source).OfType<string>().Distinct()];

        Assert.True(sources.Count > 5, $"40 campaign seeds reached only {sources.Count} programs");
    }

    [Fact]
    public void Mutate_Edits_AreTheOnesTheMutantWasMadeFrom() {
        Mutant mutant = new Mutator(1).Mutate(Rich, Mutator.Operators[0], 0)!;

        Assert.Equal(mutant.Source, SourceEdit.Apply(Rich.Source, mutant.Edits));
        Assert.NotEqual(Rich.Source, mutant.Source);
    }

    [Fact]
    public void Operators_RichSeed_EveryOperatorHasACandidate() {
        MutationContext context = MutationContext.TryCreate(Rich.Source)!;

        foreach (IMutationOperator mutationOperator in Mutator.Operators) {
            Assert.True(mutationOperator.Candidates(context).Count > 0, $"{mutationOperator.Name} found nothing in the rich seed");
        }
    }

    [Fact]
    public void Mutate_RichSeed_EveryOperatorMakesAMutantTheLexerCanScan() {
        var mutator = new Mutator(9);

        foreach (IMutationOperator mutationOperator in Mutator.Operators) {
            Mutant? mutant = mutator.Mutate(Rich, mutationOperator, 0);

            Assert.NotNull(mutant);
            Assert.NotNull(MutationContext.TryCreate(mutant!.Source));
        }
    }

    [Fact]
    public void Mutate_OperatorWithNoCandidate_GivesNull() {
        var seed = new Seed("plain@1", SeedKind.Fixture, "plain", "print(1)\n");

        Assert.Null(new Mutator(1).Mutate(seed, Mutator.Operators.Single(o => o.Name == "remove-annotation"), 0));
    }

    [Fact]
    public void Mutate_EditThatChangesNothing_GivesNull() {
        var identity = new FixedOperator("identity", c => [SourceEdit.Replace(0, 1, c.Source[..1])]);

        Assert.Null(new Mutator(1).Mutate(Rich, identity, 0));
    }

    [Fact]
    public void Mutate_RaisingNestingByTwo_IsKept() {
        var seed = new Seed("n@1", SeedKind.Fixture, "n", "x := 1\n");
        var twoDeeper = new FixedOperator("two-deeper", c => [SourceEdit.Replace(5, 6, "((1))")]);

        Mutant? mutant = new Mutator(1).Mutate(seed, twoDeeper, 0);

        Assert.Equal("x := ((1))\n", mutant!.Source);
        Assert.Equal(Mutator.MaxNestingIncrease, Mutator.MaxDepth(mutant.Source) - Mutator.MaxDepth(seed.Source));
    }

    [Fact]
    public void Mutate_RaisingNestingByThree_IsRefused() {
        var seed = new Seed("n@1", SeedKind.Fixture, "n", "x := 1\n");
        var threeDeeper = new FixedOperator("three-deeper", c => [SourceEdit.Replace(5, 6, "(((1)))")]);

        Assert.Null(new Mutator(1).Mutate(seed, threeDeeper, 0));
    }

    [Fact]
    public void TryCreate_SourceTheFrontEndCannotRead_GivesNull() {
        // A seed that makes the lexer or parser throw is the oracle's finding to report; it must not
        // stop the sample being made.
        Assert.Null(MutationContext.TryCreate(null!));
    }

    [Fact]
    public void MaxDepth_SourceTheLexerCannotRead_IsZero() {
        Assert.Equal(0, Mutator.MaxDepth(null!));
    }

    [Fact]
    public void EnclosingStatement_NodeWithNoStatementAboveIt_IsNull() {
        MutationContext context = MutationContext.TryCreate("x := 1\n")!;

        Assert.Null(context.EnclosingStatement(context.Unit));
    }

    [Fact]
    public void EnclosingStatement_Expression_IsItsStatement() {
        MutationContext context = MutationContext.TryCreate("x := 1 + 2\n")!;
        AstEntry binary = context.Nodes.First(e => e.Node is Grob.Compiler.Ast.Expressions.BinaryExpr);

        Assert.IsType<Grob.Compiler.Ast.Statements.VarDeclStmt>(context.EnclosingStatement(binary.Node));
    }

    [Fact]
    public void MaxDepth_CountsBracketNesting() {
        Assert.Equal(0, Mutator.MaxDepth("x := 1\n"));
        Assert.True(Mutator.MaxDepth("x := f(g([1]))\n") > Mutator.MaxDepth("x := f(1)\n"));
    }
}
