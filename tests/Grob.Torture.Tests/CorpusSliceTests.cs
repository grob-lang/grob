using System.Diagnostics;

using Xunit;
using Xunit.Abstractions;

using Grob.Integration.Tests.ErrorExamples;

namespace Grob.Torture.Tests;

/// <summary>
/// The CI slice (prompt §2 group 2): the unmutated seed corpus through the oracle at the CI
/// budget, after a warm-up pass. It fails on any finding the known-findings list does not name
/// by program, on a stale entry and on an entry whose register row is missing or closed.
/// </summary>
public sealed class CorpusSliceTests(SliceFixture slice, ITestOutputHelper output) : IClassFixture<SliceFixture> {
    [Fact]
    public void Slice_SeedCorpus_HasNoUntriagedFinding() {
        output.WriteLine($"{slice.Results.Count} programs in {slice.Elapsed.TotalSeconds:F1} s");
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
        foreach (SeedKind kind in Enum.GetValues<SeedKind>()) {
            int count = slice.Seeds.Count(s => s.Kind == kind);
            output.WriteLine($"{kind}: {count}");
            Assert.True(count > 0, $"no seeds of kind {kind}");
        }
    }

    [Fact]
    public void Slice_SecondRun_RunsTheSameProgramsWithTheSameFindings() {
        IReadOnlyList<ProgramResult> again = CorpusSlice.Run(slice.Seeds, new Oracle(OracleBudget.Ci));

        Assert.Equal(slice.Results.Select(r => r.ProgramId), again.Select(r => r.ProgramId));
        Assert.Equal(FindingKeys(slice.Results), FindingKeys(again));
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
        Known = KnownFindings.Parse(File.ReadAllText(Path.Join(Root, CorpusSlice.KnownFindingsPath)));

        // Warm the JIT over every tenth seed at a generous bound, so the measured pass meets the
        // CI budget of a warm process (gate report §4.4). A tenth reaches every phase and most
        // of the stdlib; the rest of the corpus adds time, not warmth.
        CorpusSlice.Run([.. Seeds.Where((_, i) => i % 10 == 0)],
            new Oracle(new OracleBudget(TimeSpan.FromMilliseconds(250), TimeSpan.FromSeconds(10))));

        var clock = Stopwatch.StartNew();
        Results = CorpusSlice.Run(Seeds, new Oracle(OracleBudget.Ci));
        Elapsed = clock.Elapsed;
    }

    internal string Root { get; }

    internal IReadOnlyList<Seed> Seeds { get; }

    internal KnownFindings Known { get; }

    internal IReadOnlyList<ProgramResult> Results { get; }

    internal TimeSpan Elapsed { get; }
}
