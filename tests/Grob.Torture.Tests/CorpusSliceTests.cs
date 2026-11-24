using System.Diagnostics;

using Xunit;
using Xunit.Abstractions;

using Grob.Integration.Tests.ErrorExamples;
using Grob.Torture.Mutation;

namespace Grob.Torture.Tests;

/// <summary>
/// The CI slice (prompt §2 groups 2 and 4): the seed corpus and a fixed sample of its mutants
/// through the oracle at the CI budget, after a warm-up pass. It fails on any finding the
/// known-findings list does not name by program, on a stale entry and on an entry whose register
/// row is missing or closed.
/// </summary>
public sealed class CorpusSliceTests(SliceFixture slice, ITestOutputHelper output) : IClassFixture<SliceFixture> {
    [Fact]
    public void Slice_CorpusAndMutants_HaveNoUntriagedFinding() {
        output.WriteLine($"{slice.Seeds.Count} seeds and {slice.Programs.Count - slice.Seeds.Count} mutants: {slice.Results.Count} programs in {slice.Elapsed.TotalSeconds:F1} s");
        foreach (IGrouping<OutcomeKind, ProgramResult> group in slice.Results.GroupBy(r => r.Outcome).OrderBy(g => g.Key)) {
            output.WriteLine($"  {group.Key}: {group.Count()}");
        }

        SliceVerdict verdict = CorpusSlice.Judge(slice.Results, slice.Known);

        Assert.True(verdict.Unknown.Count == 0, "Untriaged findings:\n" + string.Join('\n',
            verdict.Unknown.Select(u => $"  {u.Program}  {u.Finding.Signature.Key}  {u.Finding.Message}")));
        Assert.True(verdict.Stale.Count == 0, "Stale known entries:\n  " + string.Join("\n  ", verdict.Stale));
    }

    [Fact]
    public void Slice_KnownFindings_NameOpenRegisterRows() {
        string register = File.ReadAllText(Path.Join(slice.Root, CorpusSlice.RegisterPath));

        Assert.Empty(slice.Known.Problems(DeferredWorkRegister.Parse(register)));
    }

    [Fact]
    public void Slice_SeedCorpus_DrawsOnEveryLocation() {
        // A generated program is not read from the repository, and the slice does not run any yet.
        foreach (SeedKind kind in Enum.GetValues<SeedKind>().Where(k => k != SeedKind.Generated)) {
            int count = slice.Seeds.Count(s => s.Kind == kind);
            output.WriteLine($"{kind}: {count}");
            Assert.True(count > 0, $"no seeds of kind {kind}");
        }
    }

    [Fact]
    public void Slice_Mutants_ComeFromEveryOperator() {
        IEnumerable<string> operators = slice.Programs.Skip(slice.Seeds.Count).Select(p => p.Id.Split('/')[^2]);

        Assert.Equal([.. Mutator.Operators.Select(o => o.Name).Order(StringComparer.Ordinal)], [.. operators.Distinct().Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public void Slice_Mutants_AreAFixedSample() {
        IReadOnlyList<Seed> again = CorpusSlice.Programs(slice.Seeds);

        Assert.Equal(slice.Programs.Select(p => (p.Id, p.Source)), again.Select(p => (p.Id, p.Source)));
        Assert.InRange(slice.Programs.Count - slice.Seeds.Count, 300, 1500);
    }

    [Fact]
    public void Slice_SecondRun_RunsTheSameProgramsWithTheSameFindings() {
        Assert.False(CorpusSlice.TimedOut(slice.Results), "the first pass timed out, so no second pass may start beside it");

        // The second pass runs every program that made a finding and every tenth of the rest: a whole
        // second pass would double the slice's time to show the same thing.
        HashSet<string> withFindings = [.. slice.Results.Where(r => r.Findings.Count > 0).Select(r => r.ProgramId)];
        List<Seed> rerun = [.. slice.Programs.Where((p, i) => i % 10 == 0 || withFindings.Contains(p.Id))];
        IReadOnlyList<ProgramResult> again = CorpusSlice.Run(rerun, new Oracle(OracleBudget.Ci));

        HashSet<string> rerunIds = [.. rerun.Select(p => p.Id)];
        Assert.Equal(rerun.Select(p => p.Id), again.Select(r => r.ProgramId));
        Assert.Equal(FindingKeys([.. slice.Results.Where(r => rerunIds.Contains(r.ProgramId))]), FindingKeys(again));
    }

    [Fact]
    public void Run_TimeoutFinding_StopsTheSliceAtThatProgram() {
        using var release = new ManualResetEventSlim();
        var budget = new OracleBudget(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(500));
        var oracle = new Oracle(budget, phaseHook: p => {
            if (p == PipelinePhase.Parse) release.Wait();
        });
        Seed[] seeds = [new("a@1", SeedKind.Fixture, "a", "print(1)\n"), new("b@2", SeedKind.Fixture, "b", "print(2)\n")];

        try {
            ProgramResult only = Assert.Single(CorpusSlice.Run(seeds, oracle));

            Assert.Equal("a@1", only.ProgramId);
            Assert.Equal(FindingClass.Timeout, Assert.Single(only.Findings).Signature.Class);
        } finally {
            release.Set();
        }
    }

    // Bounded runs are left out: a slow runner can tip a slow program from clean to bounded,
    // which is not a change in what the program does (gate report §4.4).
    private static List<string> FindingKeys(IReadOnlyList<ProgramResult> results) =>
        [.. results.SelectMany(r => r.Findings.Select(f => $"{r.ProgramId} {f.Signature.Key}"))];
}

/// <summary>Runs the slice once for the class: one warm-up pass, then the measured pass.</summary>
public sealed class SliceFixture {
    public SliceFixture() {
        Root = SeedCorpus.LocateRepositoryRoot(AppContext.BaseDirectory);
        Seeds = SeedCorpus.Load(Root);
        Programs = CorpusSlice.Programs(Seeds);
        Known = KnownFindings.Parse(File.ReadAllText(Path.Join(Root, CorpusSlice.KnownFindingsPath)));

        // Warm the JIT over every tenth seed at a generous bound, so the measured pass meets the
        // CI budget of a warm process (gate report §4.4). A tenth reaches every phase and most
        // of the stdlib; the rest of the corpus adds time, not warmth.
        // A timeout leaves a worker that cannot be stopped, so a warm-up that ends in one becomes
        // the slice's result and no measured pass starts beside it.
        IReadOnlyList<ProgramResult> warmUp = CorpusSlice.Run([.. Seeds.Where((_, i) => i % 10 == 0)],
            new Oracle(new OracleBudget(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(10))));
        if (CorpusSlice.TimedOut(warmUp)) {
            Results = warmUp;
            return;
        }

        var clock = Stopwatch.StartNew();
        Results = CorpusSlice.Run(Programs, new Oracle(OracleBudget.Ci));
        Elapsed = clock.Elapsed;
    }

    internal string Root { get; }

    internal IReadOnlyList<Seed> Seeds { get; }

    internal IReadOnlyList<Seed> Programs { get; }

    internal KnownFindings Known { get; }

    internal IReadOnlyList<ProgramResult> Results { get; }

    internal TimeSpan Elapsed { get; }
}
