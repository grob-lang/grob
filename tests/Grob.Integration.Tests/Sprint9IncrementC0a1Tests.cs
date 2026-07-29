using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 Increment C0a-1 (D-371) integration tests: the array non-mutating query
/// member surface — <c>length</c>, <c>isEmpty</c>, <c>first()</c>, <c>last()</c>,
/// <c>contains(v)</c> — and <c>sort</c>'s new <c>date</c>/<c>guid</c> comparer arms, end
/// to end through the full pipeline (lex -&gt; parse -&gt; type-check -&gt; compile -&gt;
/// VM, stdlib plugins auto-registered at startup).
/// </summary>
public sealed class Sprint9IncrementC0a1Tests {
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
    // length / isEmpty
    // -----------------------------------------------------------------------

    [Fact]
    public void Length_And_IsEmpty_OnPopulatedArray() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [10, 20, 30]
            print(xs.length)
            print(xs.isEmpty)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("3" + NL + "false" + NL, stdout);
    }

    [Fact]
    public void Length_And_IsEmpty_OnEmptyArray() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs: int[] := []
            print(xs.length)
            print(xs.isEmpty)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("0" + NL + "true" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // first() / last()
    // -----------------------------------------------------------------------

    [Fact]
    public void First_And_Last_OnPopulatedArray_ReturnEndpoints() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [10, 20, 30]
            print(xs.first())
            print(xs.last())
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("10" + NL + "30" + NL, stdout);
    }

    [Fact]
    public void First_And_Last_OnEmptyArray_ReturnNil_ConsumableViaNilCoalesce() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs: int[] := []
            print(xs.first() ?? -1)
            print(xs.last() ?? -1)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("-1" + NL + "-1" + NL, stdout);
    }

    [Fact]
    public void First_OnEmptyArray_OptionalChain_PrintsNil() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs: int[] := []
            v := xs.first()
            print(v)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("nil" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // contains(v)
    // -----------------------------------------------------------------------

    [Fact]
    public void Contains_ElementPresentAndAbsent() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := ["apple", "banana", "cherry"]
            print(xs.contains("banana"))
            print(xs.contains("kiwi"))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("true" + NL + "false" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // contains(v) — date equality parity, the load-bearing test (D-371). A date[]
    // containing 'a' at a non-UTC offset must still find 'a.toUtc()' — the SAME
    // instant expressed at a DIFFERENT offset — matching D-367's '==' behaviour
    // exactly. A contains() that disagreed with '==' would be the trichotomy bug
    // in a new place.
    // -----------------------------------------------------------------------

    [Fact]
    public void Contains_DateArray_ToUtcOfContainedValue_IsTrue() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := date.parse("2026-01-01T13:00:00+01:00")
            xs := [a]
            print(xs.contains(a.toUtc()))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("true" + NL, stdout);
    }

    [Fact]
    public void Contains_DateArray_DifferentInstant_IsFalse() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := date.parse("2026-01-01T13:00:00+01:00")
            b := date.parse("2026-01-02T13:00:00+01:00")
            xs := [a]
            print(xs.contains(b))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("false" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // sort — date/guid comparer arms (D-371).
    // -----------------------------------------------------------------------

    [Fact]
    public void Sort_ByDateKey_OrdersByInstant_AcrossDifferingOffsets() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := date.parse("2026-01-02T09:00:00-05:00")
            b := date.parse("2026-01-01T00:00:00Z")
            c := date.parse("2026-01-01T13:00:00+02:00")
            xs := [a, b, c]
            sorted := xs.sort(x => x)
            print(sorted[0])
            print(sorted[1])
            print(sorted[2])
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        // Ascending by instant: b (00:00 UTC) < c (11:00 UTC) < a (14:00 UTC, next day).
        Assert.Equal(
            "2026-01-01T00:00:00Z" + NL +
            "2026-01-01T13:00:00+02:00" + NL +
            "2026-01-02T09:00:00-05:00" + NL,
            stdout);
    }

    [Fact]
    public void Sort_ByGuidKey_OrdersOrdinally() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            g1 := guid.parse("33333333-3333-3333-3333-333333333333")
            g2 := guid.parse("11111111-1111-1111-1111-111111111111")
            g3 := guid.parse("22222222-2222-2222-2222-222222222222")
            xs := [g1, g2, g3]
            sorted := xs.sort(x => x)
            print(sorted[0])
            print(sorted[1])
            print(sorted[2])
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal(
            "11111111-1111-1111-1111-111111111111" + NL +
            "22222222-2222-2222-2222-222222222222" + NL +
            "33333333-3333-3333-3333-333333333333" + NL,
            stdout);
    }

    // -----------------------------------------------------------------------
    // sort — uncomparable key kind is a catchable GrobError (correctness batch, D-382).
    // Previously the comparator's fault bypassed the native-throw seam entirely and
    // could not be caught from Grob source at all; it is now routed through the same
    // seam array insert/remove and the string natives already use.
    // -----------------------------------------------------------------------

    [Fact]
    public void Sort_ByUncomparableArrayKey_CaughtByTryCatch() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [[1], [2]]
            try {
                sorted := xs.sort(x => x)
                print(sorted[0][0])
            } catch (e: RuntimeError) {
                print("caught")
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("caught" + NL, stdout);
    }
}
