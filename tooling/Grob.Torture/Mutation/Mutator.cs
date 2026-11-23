using Grob.Compiler;
using Grob.Core;
using Grob.Torture.Fakes;
using Grob.Torture.Mutation.Operators;

namespace Grob.Torture.Mutation;

/// <summary>
/// Makes one mutant: a seed, an operator and a draw number give one program, the same one every
/// time (prompt §3, seeds stay fixed). The PRNG is SplitMix64 seeded from the campaign seed and the
/// program id by FNV-1a, never <see cref="Random"/> or <see cref="string.GetHashCode()"/>, so a
/// register row's program id still reproduces after a .NET upgrade (gate report §4.5).
/// </summary>
/// <param name="campaignSeed">The campaign's seed. The CI slice's is a constant in its test.</param>
internal sealed class Mutator(ulong campaignSeed) {
    /// <summary>The most a mutant may raise a seed's bracket nesting by (gate report §1.6): it keeps F-3, the parser's missing depth guard, out of the slice.</summary>
    public const int MaxNestingIncrease = 2;

    /// <summary>The twelve operators, in the order the gate report numbers them.</summary>
    public static IReadOnlyList<IMutationOperator> Operators { get; } = [
        new RemoveAnnotation(), new ReplaceValue(), new MakeNullable(), new LambdaOperand(),
        new SwapOperands(), new SwapIdentifier(), new RouteValue(), new ChangeOptionalChain(),
        new DeleteStatement(), new EditArgument(), new ConstEdge(), new NullableReceiver(),
    ];

    /// <summary>The program id a seed, an operator and a draw give.</summary>
    public static string ProgramId(string seedId, string operatorName, int draw) => $"{seedId}/{operatorName}/{draw}";

    /// <summary>
    /// The mutant <paramref name="seed"/>, <paramref name="mutationOperator"/> and
    /// <paramref name="draw"/> give, or <see langword="null"/> when the seed cannot be read, the
    /// operator has no candidate in it, the edit changes nothing or the result nests too deeply.
    /// </summary>
    public Mutant? Mutate(Seed seed, IMutationOperator mutationOperator, int draw) {
        MutationContext? context = MutationContext.TryCreate(seed.Source);
        if (context is null) return null;

        IReadOnlyList<IReadOnlyList<SourceEdit>> candidates = mutationOperator.Candidates(context);
        if (candidates.Count == 0) return null;

        string programId = ProgramId(seed.Id, mutationOperator.Name, draw);
        var random = new SplitMixRandomSource(unchecked((long)SeedCorpus.Fnv1a64($"{campaignSeed:x16}|{programId}")));
        IReadOnlyList<SourceEdit> edits = candidates[(int)random.NextInt(0, candidates.Count - 1)];

        string source = SourceEdit.Apply(seed.Source, edits);
        if (source == seed.Source || MaxDepth(source) > MaxDepth(seed.Source) + MaxNestingIncrease) return null;
        return new Mutant(programId, seed.Id, mutationOperator.Name, draw, source, edits);
    }

    /// <summary>The deepest bracket nesting the lexer records in <paramref name="source"/>.</summary>
    public static int MaxDepth(string source) {
        try {
            return Lexer.Scan(source, new DiagnosticBag(), "depth.grob").Max(t => t.BracketDepth);
        } catch (Exception) {
            return 0;
        }
    }
}
