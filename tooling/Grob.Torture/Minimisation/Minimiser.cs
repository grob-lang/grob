using Grob.Torture.Mutation;
using Grob.Torture.Mutation.Operators;

namespace Grob.Torture.Minimisation;

/// <summary>
/// Delta debugging over a failing program: a pass over lines, then passes over syntax (prompt
/// §1.4; gate report §4.6). A candidate is kept only if it still fails the way the original did, so
/// the failure's signature is kept while the source shrinks. It does not keep the root cause, since
/// two causes can share a signature; so the program is reduced twice, from the front and from the
/// back, and the two results are compared, and the lines of the reproducer are taken out of the
/// original to see whether it still fails without them.
/// </summary>
/// <remarks>
/// The line pass tries each complement of a chunk, as Zeller's ddmin does, and not the chunks
/// themselves: the result is 1-minimal for the chunks tried and not always the smallest program.
/// The syntax passes reuse the mutation operators that make a program shorter (a deleted
/// statement, a dropped argument, an expression replaced by a literal, a removed annotation or
/// guard) and accept an edit only if the result is strictly shorter, so they end.
/// </remarks>
/// <param name="stillFails">Whether a candidate still fails with the signature being minimised.</param>
/// <param name="maxTests">The most candidates to run; the minimiser stops at this and returns what it has.</param>
internal sealed class Minimiser(Func<string, bool> stillFails, int maxTests = 2000) {
    private static readonly IMutationOperator[] Reducers = [
        new DeleteStatement(), new EditArgument(), new ReplaceValue(), new RemoveAnnotation(), new ChangeOptionalChain(),
    ];

    private int _tests;
    private readonly Dictionary<string, bool> _answers = new(StringComparer.Ordinal);

    /// <summary>Minimises <paramref name="source"/>, which must fail.</summary>
    public MinimisationResult Minimise(string source) {
        string[] lines = SplitLines(source);
        IReadOnlyList<int> keptForward = LineDdmin(lines, fromBack: false);
        string forward = SyntaxPasses(Join(lines, keptForward), fromBack: false);
        string backward = SyntaxPasses(Join(lines, LineDdmin(lines, fromBack: true)), fromBack: true);

        string remainder = Join(lines, [.. Enumerable.Range(0, lines.Length).Except(keptForward)]);
        bool secondCause = remainder.Trim().Length > 0 && Fails(remainder);

        return new MinimisationResult(source, forward, backward, ConstructSet(forward) != ConstructSet(backward), secondCause, _tests);
    }

    private bool Fails(string candidate) {
        if (_answers.TryGetValue(candidate, out bool known)) return known;
        if (_tests >= maxTests) return false;
        _tests++;
        return _answers[candidate] = stillFails(candidate);
    }

    // ---- lines ------------------------------------------------------------------------------

    private IReadOnlyList<int> LineDdmin(string[] lines, bool fromBack) {
        List<int> kept = [.. Enumerable.Range(0, lines.Length)];
        int granularity = 2;
        while (kept.Count >= 2) {
            IReadOnlyList<List<int>> chunks = Chunks(kept, granularity);
            IEnumerable<List<int>> order = fromBack ? chunks.Reverse() : chunks;

            List<int>? smaller = null;
            foreach (List<int> chunk in order) {
                List<int> complement = [.. kept.Except(chunk)];
                if (complement.Count > 0 && Fails(Join(lines, complement))) {
                    smaller = complement;
                    break;
                }
            }

            if (smaller is not null) {
                kept = smaller;
                granularity = Math.Max(granularity - 1, 2);
            } else if (granularity >= kept.Count) {
                break;
            } else {
                granularity = Math.Min(kept.Count, granularity * 2);
            }
        }
        return kept;
    }

    private static List<List<int>> Chunks(List<int> kept, int count) {
        var chunks = new List<List<int>>();
        int size = (int)Math.Ceiling(kept.Count / (double)count);
        for (int start = 0; start < kept.Count; start += size) {
            chunks.Add(kept.GetRange(start, Math.Min(size, kept.Count - start)));
        }
        return chunks;
    }

    private static string[] SplitLines(string source) {
        List<string> lines = [.. source.Split('\n').Select(l => l + "\n")];
        if (!source.EndsWith('\n')) lines[^1] = lines[^1].TrimEnd('\n');
        else lines.RemoveAt(lines.Count - 1);
        return [.. lines];
    }

    private static string Join(string[] lines, IReadOnlyList<int> kept) => string.Concat(kept.Select(i => lines[i]));

    // ---- syntax -----------------------------------------------------------------------------

    private string SyntaxPasses(string source, bool fromBack) {
        string current = source;
        while (true) {
            string? next = FirstShorterThatFails(current, fromBack);
            if (next is null) return current;
            current = next;
        }
    }

    private string? FirstShorterThatFails(string current, bool fromBack) {
        MutationContext? context = MutationContext.TryCreate(current);
        if (context is null) return null;

        IEnumerable<string> candidates = Reducers
            .SelectMany(r => r.Candidates(context))
            .Select(edits => SourceEdit.Apply(current, edits))
            .Where(c => c.Length < current.Length);
        if (fromBack) candidates = candidates.Reverse();
        return candidates.FirstOrDefault(Fails);
    }

    // The set of syntax node kinds a program holds, so two reproducers can be compared by what
    // they are made of and not by how they are spelt.
    private static string ConstructSet(string source) {
        MutationContext? context = MutationContext.TryCreate(source);
        return context is null
            ? string.Empty
            : string.Join(',', context.Nodes.Select(e => e.Node.GetType().Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }
}
