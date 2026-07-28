using System.Text;

using Grob.Cli;
using Grob.Core;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 Increment C0b-2b (D-378) integration tests: the map in-place-mutating member
/// surface — <c>set(key, value)</c>, <c>remove(key)</c>, <c>clear()</c> — end to end
/// through the full pipeline (lex -> parse -> type-check -> compile -> VM, stdlib plugins
/// auto-registered at startup). Also proves D-372's aliasing ratification end to end, the
/// compile-time <c>readonly</c> rejection (E0204) at a method-call site, and the
/// <c>for k, v in m</c> mutation-during-iteration behaviour (reported, not fixed — see
/// D-378: the loop's iteration count is fixed by a one-time <c>keys</c> snapshot, unlike
/// the array's shifting bound, but a removed/cleared key still yields a nil value read).
/// </summary>
public sealed class Sprint9IncrementC0b2bTests {
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
    // set(key, value) — observed via D-377's query members.
    // -----------------------------------------------------------------------

    [Fact]
    public void Set_NewKey_GrowsMap_ObservedViaLengthAndContains() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1}
            m.set("b", 2)
            print(m.length)
            print(m.contains("b"))
            print(m.get("b"))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("2" + NL + "true" + NL + "2" + NL, stdout);
    }

    [Fact]
    public void Set_ExistingKey_OverwritesValue_LengthUnchanged() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1}
            m.set("a", 99)
            print(m.length)
            print(m.get("a"))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("1" + NL + "99" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // remove(key) — no-op if absent.
    // -----------------------------------------------------------------------

    [Fact]
    public void Remove_ExistingKey_ShrinksMap_ObservedViaLengthAndContains() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1, "b": 2}
            m.remove("a")
            print(m.length)
            print(m.contains("a"))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("1" + NL + "false" + NL, stdout);
    }

    [Fact]
    public void Remove_AbsentKey_IsNoOp_LengthUnchanged_NoError() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1}
            m.remove("nope")
            print(m.length)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("1" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // clear()
    // -----------------------------------------------------------------------

    [Fact]
    public void Clear_EmptiesMap_LengthZero_IsEmptyTrue_KeysValuesEmpty() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1, "b": 2}
            m.clear()
            print(m.length)
            print(m.isEmpty)
            print(m.keys.length)
            print(m.values.length)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("0" + NL + "true" + NL + "0" + NL + "0" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Ordering — load-bearing (D-377's guarantee, must survive `set`).
    // -----------------------------------------------------------------------

    [Fact]
    public void Set_NewKey_AppearsLastInKeys_ExistingKeyKeepsPosition() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"z": 1, "a": 2}
            m.set("a", 20)
            m.set("new", 3)
            print(m.keys[0])
            print(m.keys[1])
            print(m.keys[2])
            print(m.values[1])
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("z" + NL + "a" + NL + "new" + NL + "20" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // readonly rejection at compile time (E0204) — for all three members.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("readonly m := map<string, int>{\"a\": 1}\nm.set(\"b\", 2)\n")]
    [InlineData("readonly m := map<string, int>{\"a\": 1}\nm.remove(\"a\")\n")]
    [InlineData("readonly m := map<string, int>{\"a\": 1}\nm.clear()\n")]
    public void MutatingCall_OnReadonlyBinding_CompileTimeE0204(string source) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.NotEqual(0, exitCode);
        Assert.Equal("", stdout);
        Assert.Contains("E0204", stderr);
    }

    // -----------------------------------------------------------------------
    // Aliasing — the test the whole D-372 prerequisite existed to make possible.
    // -----------------------------------------------------------------------

    [Fact]
    public void Set_ThroughSecondBinding_VisibleThroughFirst() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := map<string, int>{"x": 1}
            b := a
            b.set("y", 2)
            print(a.length)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("2" + NL, stdout);
    }

    [Fact]
    public void Set_ThroughFunctionParameter_VisibleToCallerAfterReturn() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            fn addEntry(m: map<string, int>): void {
                m.set("added", 1)
            }
            a := map<string, int>{"x": 1}
            addEntry(a)
            print(a.length)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("2" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // for k, v in m during mutation — reported behaviour, not a bug fix. Unlike the
    // array's for...in (which re-reads its bound every iteration, D-373), the map's
    // for...in materialises `keys` ONCE, so the iteration count here stays fixed at 2
    // even though `remove` shrinks the map mid-loop — but the removed key's value read
    // degrades to nil rather than the loop skipping it or throwing.
    // -----------------------------------------------------------------------

    [Fact]
    public void ForIn_RemovingCurrentMapDuringIteration_KeepsFixedIterationCount_ValueReadsNilForRemovedKey() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            m := map<string, int>{"a": 1, "b": 2}
            count := 0
            for k, v in m {
                count = count + 1
                m.remove("b")
                print(v)
            }
            print(count)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        // Both iterations run (count reaches 2, the keys snapshot's original size), but
        // the second iteration's `v` (for key "b", removed on the first iteration) reads
        // nil rather than 2.
        Assert.Equal("1" + NL + "nil" + NL + "2" + NL, stdout);
    }
}
