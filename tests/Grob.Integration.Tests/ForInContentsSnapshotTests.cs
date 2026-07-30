using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// D-383 (refining D-379/D-378) integration tests: <c>for...in</c> snapshots the
/// collection's contents at loop entry, for both arrays and maps. Arrays copy the
/// element sequence instead of re-reading a live bound each iteration (D-373's
/// unbounded-append/early-exit finding); maps copy key-value pairs instead of keys
/// alone (D-378's recorded soundness hole, where a removed key's value read
/// degraded to <c>nil</c> against the non-nullable <c>V</c> binding, D-374). The
/// copy is shallow (D-372 reference semantics) — mutating an element's own
/// contents stays visible; only the sequence/entry set is frozen.
/// </summary>
public sealed class ForInContentsSnapshotTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    private static string NL => Environment.NewLine;

    // -----------------------------------------------------------------------
    // Arrays — append no longer iterates unboundedly; removal no longer faults.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayAppendDuringIteration_VisitsExactlyTheEntryCount_Terminates() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2, 3]
            count := 0
            for x in xs {
                count = count + 1
                xs.append(99)
            }
            print(count)
            print(xs.length)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        // The loop visits exactly the 3 elements present at entry; each iteration's
        // append grows the live array but has no effect on the frozen snapshot, so
        // the live array ends up with 3 original + 3 appended = 6 elements.
        Assert.Equal("3" + NL + "6" + NL, stdout);
    }

    [Fact]
    public void ArrayRemovalDuringIteration_VisitsEntryTimeValues_DoesNotFault() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2, 3]
            results: int[] := []
            for x in xs {
                results.append(x)
                xs.remove(0)
            }
            print(results.length)
            for r in results {
                print(r)
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        // A count-only (length) snapshot would have indexed a shrinking live array
        // and faulted on the third iteration (D-383's motivating example). The
        // contents snapshot visits exactly the three entry-time values instead.
        Assert.Equal("3" + NL + "1" + NL + "2" + NL + "3" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Maps — the soundness test: v never degrades to nil against its non-nullable
    // binding, even across both remove and clear during the same iteration.
    // -----------------------------------------------------------------------

    [Fact]
    public void MapSoundness_RemovingAndClearingDuringIteration_ValueNeverDegradesToNil() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1, "b": 2, "c": 3}
            results: int[] := []
            for k, v in m {
                results.append(v)
                m.remove(k)
                m.clear()
            }
            print(results.length)
            for r in results {
                print(r)
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        // Every iteration's v is read from the entry-time values snapshot, so
        // neither the per-iteration remove nor the clear() (which empties the live
        // map entirely on the first pass) has any effect on what is visited or read.
        Assert.Equal("3" + NL + "1" + NL + "2" + NL + "3" + NL, stdout);
    }

    [Fact]
    public void MapValueUpdatedMidLoop_LaterIterationSeesTheEntryTimeValue() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1, "b": 2}
            for k, v in m {
                if (k == "a") {
                    m.set("b", 99)
                }
                if (k == "b") {
                    print(v)
                }
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        // "b" is set to 99 while processing "a", but the "b" iteration's v was
        // snapshotted at loop entry — it reads 2, never the mid-loop update.
        Assert.Equal("2" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Shallow-copy semantics (D-372) — element mutation is visible; the sequence
    // itself is frozen.
    // -----------------------------------------------------------------------

    [Fact]
    public void ShallowCopy_MutatingElementContentsVisible_AppendingOuterArrayNotVisited() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            rows := [[1], [2]]
            for r in rows {
                r.append(9)
                rows.append([3])
            }
            print(rows.length)
            print(rows[0].length)
            print(rows[0][1])
            print(rows[1].length)
            print(rows[1][1])
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        // rows.append([3]) inside the loop is never visited by the loop itself —
        // the loop still runs exactly 2 iterations (the entry-time count), even
        // though it fires twice and grows the live rows to 4 elements — but
        // r.append(9) mutates the same GrobArray reference held by rows, so it IS
        // visible on both entry-time rows.
        Assert.Equal("4" + NL + "2" + NL + "9" + NL + "2" + NL + "9" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Nesting — no synthetic-local collision, same array or different collections.
    // -----------------------------------------------------------------------

    [Fact]
    public void NestedForIn_SameArray_NoSyntheticLocalCollision() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2]
            for a in xs {
                for b in xs {
                    print(a * 10 + b)
                }
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("11" + NL + "12" + NL + "21" + NL + "22" + NL, stdout);
    }

    [Fact]
    public void NestedForIn_ArrayInsideMap_NoSyntheticLocalCollision() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1, "b": 2}
            xs := [10, 20]
            for k, v in m {
                for x in xs {
                    print(k)
                    print(v + x)
                }
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal(
            "a" + NL + "11" + NL + "a" + NL + "21" + NL +
            "b" + NL + "12" + NL + "b" + NL + "22" + NL,
            stdout);
    }

    // -----------------------------------------------------------------------
    // break/continue over the snapshot — clean exit, no stack residue; a captured
    // loop variable still closes correctly.
    // -----------------------------------------------------------------------

    [Fact]
    public void BreakOverArraySnapshot_ExitsCleanly_SubsequentCodeRunsCorrectly() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2, 3, 4]
            for x in xs {
                if (x == 3) {
                    break
                }
                print(x)
            }
            print("done")
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("1" + NL + "2" + NL + "done" + NL, stdout);
    }

    [Fact]
    public void ContinueOverMapSnapshot_SkipsCorrectly_NoStackResidue() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1, "b": 2, "c": 3}
            for k, v in m {
                if (k == "b") {
                    continue
                }
                print(v)
            }
            print("done")
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("1" + NL + "3" + NL + "done" + NL, stdout);
    }

    [Fact]
    public void MapForInBodyCapture_PerIterationValueClosesOnBodyExit() {
        // Mirrors Sprint5IncrementDTests's array for...in capture tests, over the
        // map form's now four synthetic locals — a lambda capturing the value
        // variable must still close its own upvalue correctly each iteration.
        (string stdout, string stderr, int exitCode) = RunSource("""
            fn lastValue(): int {
                held := () => 0
                m := map<string, int>{"a": 1, "b": 2, "c": 3}
                for k, v in m {
                    held = () => v
                }
                out := 0
                out = held()
                return out
            }
            print(lastValue())
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        // The last iteration ("c") captured v = 3.
        Assert.Equal("3" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Numeric range for...in — untouched by this change (no collection involved).
    // -----------------------------------------------------------------------

    [Fact]
    public void NumericRangeForIn_UnaffectedByTheSnapshotChange() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            for i in 0..3 {
                print(i)
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("0" + NL + "1" + NL + "2" + NL + "3" + NL, stdout);
    }
}
