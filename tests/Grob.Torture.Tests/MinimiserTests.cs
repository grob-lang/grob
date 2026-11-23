using Grob.Torture.Minimisation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The minimiser keeps a failure's signature while it shrinks the source, keeps the original beside
/// the reproducer, and notices when a reduction may have slipped between two root causes that share
/// a signature (prompt §1.4, §4).
/// </summary>
public sealed class MinimiserTests {
    // A stand-in for the oracle: a program "fails" when it holds a marker.
    private static Func<string, bool> Contains(params string[] markers) =>
        source => markers.Any(m => source.Contains(m, StringComparison.Ordinal));

    [Fact]
    public void Minimise_IrrelevantLines_AreRemoved() {
        const string source = "a := 1\nb := 2\nx := bad()\nc := 3\nd := 4\n";

        MinimisationResult result = new Minimiser(Contains("bad()")).Minimise(source);

        Assert.Equal("x := bad()\n", result.Reproducer);
    }

    [Fact]
    public void Minimise_FailureThatNeedsTwoLines_KeepsBothAndNoMore() {
        static bool NeedsBoth(string s) => s.Contains("a := 1\n", StringComparison.Ordinal) && s.Contains("b := 2\n", StringComparison.Ordinal);

        MinimisationResult result = new Minimiser(NeedsBoth).Minimise("a := 1\nx := 9\nb := 2\ny := 8\n");

        Assert.Equal("a := 1\nb := 2\n", result.Reproducer);
    }

    [Fact]
    public void Minimise_Result_KeepsTheOriginalBesideTheReproducer() {
        const string source = "a := 1\nx := bad()\n";

        MinimisationResult result = new Minimiser(Contains("bad()")).Minimise(source);

        Assert.Equal(source, result.Original);
        Assert.NotEqual(result.Original, result.Reproducer);
    }

    [Fact]
    public void Minimise_Reproducer_StillFailsTheWayTheOriginalDid() {
        Func<string, bool> fails = Contains("bad(");
        const string source = "a := 1\nfn f(): int {\n    y := 2\n    return bad(y + 1)\n}\nprint(f())\n";

        MinimisationResult result = new Minimiser(fails).Minimise(source);

        Assert.True(fails(result.Reproducer));
        Assert.True(result.Reproducer.Length < source.Length);
    }

    [Fact]
    public void Minimise_Arguments_AreDroppedWhenTheFailureDoesNotNeedThem() {
        MinimisationResult result = new Minimiser(Contains("bad(")).Minimise("x := bad(1, 2, 3)\n");

        Assert.Equal("x := bad()\n", result.Reproducer);
    }

    [Fact]
    public void Minimise_Expressions_AreReplacedByLiterals() {
        MinimisationResult result = new Minimiser(Contains("x :=")).Minimise("x := compute(1) + other(2)\n");

        Assert.True(result.Reproducer.Length < "x := compute(1) + other(2)\n".Length);
        Assert.StartsWith("x := ", result.Reproducer);
    }

    [Fact]
    public void Minimise_ASingleLine_IsKeptWhenItIsTheFailure() {
        MinimisationResult result = new Minimiser(Contains("bad")).Minimise("bad\n");

        Assert.Equal("bad\n", result.Reproducer);
    }

    [Fact]
    public void Minimise_SourceWithoutATrailingNewline_KeepsItsShape() {
        MinimisationResult result = new Minimiser(Contains("bad()")).Minimise("a := 1\nx := bad()");

        Assert.Equal("x := bad()", result.Reproducer);
    }

    [Fact]
    public void Minimise_OneRootCause_IsNotFlagged() {
        MinimisationResult result = new Minimiser(Contains("bad()")).Minimise("a := 1\nx := bad()\nb := 2\n");

        Assert.False(result.DirectionsDisagree);
        Assert.False(result.SecondCauseSuspected);
        Assert.False(result.PossiblyMultiCause);
    }

    [Fact]
    public void Minimise_TwoRootCausesSharingASignature_AreFlagged() {
        // Both lines make the same signature, as R-58 and R-63 do. The reproducer keeps one of
        // them, so a reader who stopped at the reproducer would not know the other was there.
        const string source = "x := bad()\nys := [1, 2]\nz := worse([1])\n";

        MinimisationResult result = new Minimiser(Contains("bad()", "worse(")).Minimise(source);

        Assert.True(result.PossiblyMultiCause);
        Assert.True(result.SecondCauseSuspected, "the original still fails once the reproducer's lines are removed");
    }

    [Fact]
    public void Minimise_TwoRootCausesOfDifferentShape_DirectionsDisagree() {
        const string source = "x := bad()\nz := [worse]\n";

        MinimisationResult result = new Minimiser(Contains("bad()", "[worse]")).Minimise(source);

        Assert.True(result.DirectionsDisagree);
        Assert.NotEqual(result.Reproducer, result.Alternate);
    }

    [Fact]
    public void Minimise_TestBudget_StopsTheSearchAndReturnsWhatItHas() {
        string source = string.Concat(Enumerable.Range(0, 60).Select(i => $"v{i} := {i}\n")) + "x := bad()\n";

        MinimisationResult result = new Minimiser(Contains("bad()"), maxTests: 5).Minimise(source);

        Assert.True(result.Tests <= 5);
        Assert.Contains("bad()", result.Reproducer);
    }

    [Fact]
    public void Minimise_RepeatedCandidates_AreRunOnce() {
        var seen = new Dictionary<string, int>();
        bool Fails(string candidate) {
            seen[candidate] = seen.GetValueOrDefault(candidate) + 1;
            return candidate.Contains("bad()", StringComparison.Ordinal);
        }

        new Minimiser(Fails).Minimise("a := 1\nb := 2\nx := bad()\nc := 3\n");

        Assert.All(seen, kv => Assert.Equal(1, kv.Value));
    }
}
