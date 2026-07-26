using System.Text;

using Grob.Cli;
using Grob.Core;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 Increment C0a-2 (D-373) integration tests: the array in-place-mutating
/// member surface — <c>append(value)</c>, <c>insert(index, value)</c>,
/// <c>remove(index)</c>, <c>clear()</c> — end to end through the full pipeline (lex ->
/// parse -> type-check -> compile -> VM, stdlib plugins auto-registered at startup).
/// Also proves D-372's aliasing ratification end to end and the compile-time
/// <c>readonly</c> rejection (E0204) at a method-call site.
/// </summary>
public sealed class Sprint9IncrementC0a2Tests {
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
    // append(value)
    // -----------------------------------------------------------------------

    [Fact]
    public void Append_GrowsArray_ObservedViaLength() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2]
            xs.append(3)
            print(xs.length)
            print(xs[2])
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("3" + NL + "3" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // insert(index, value)
    // -----------------------------------------------------------------------

    [Fact]
    public void Insert_AtMiddleIndex_ShiftsSubsequentElements() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2, 3]
            xs.insert(1, 99)
            print(xs[0])
            print(xs[1])
            print(xs[2])
            print(xs[3])
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("1" + NL + "99" + NL + "2" + NL + "3" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // remove(index)
    // -----------------------------------------------------------------------

    [Fact]
    public void Remove_AtMiddleIndex_ShiftsSubsequentElements() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2, 3]
            xs.remove(1)
            print(xs.length)
            print(xs[0])
            print(xs[1])
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("2" + NL + "1" + NL + "3" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // clear()
    // -----------------------------------------------------------------------

    [Fact]
    public void Clear_EmptiesArray_LengthZero_IsEmptyTrue() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2, 3]
            xs.clear()
            print(xs.length)
            print(xs.isEmpty)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("0" + NL + "true" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Bounds — insert/remove out of range raise a catchable IndexError.
    // -----------------------------------------------------------------------

    [Fact]
    public void Insert_NegativeIndex_UncaughtIndexError_ExitsNonZero() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2]
            xs.insert(-1, 9)
            """);

        Assert.NotEqual(0, exitCode);
        Assert.Equal("", stdout);
        Assert.Contains(ErrorCatalog.E5101.Code, stderr);
    }

    [Fact]
    public void Insert_IndexEqualToLength_Succeeds_AppendsAtEnd() {
        // Pinned boundary rule: index == length is a valid append-position insert.
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2]
            xs.insert(2, 3)
            print(xs[2])
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("3" + NL, stdout);
    }

    [Fact]
    public void Insert_IndexGreaterThanLength_UncaughtIndexError_ExitsNonZero() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2]
            xs.insert(3, 9)
            """);

        Assert.NotEqual(0, exitCode);
        Assert.Equal("", stdout);
        Assert.Contains(ErrorCatalog.E5101.Code, stderr);
    }

    [Fact]
    public void Remove_IndexEqualToLength_UncaughtIndexError_ExitsNonZero() {
        // Pinned boundary rule: unlike insert, index == length is OUT of range for remove.
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs := [1, 2]
            xs.remove(2)
            """);

        Assert.NotEqual(0, exitCode);
        Assert.Equal("", stdout);
        Assert.Contains(ErrorCatalog.E5101.Code, stderr);
    }

    [Fact]
    public void Remove_OnEmptyArray_UncaughtIndexError_ExitsNonZero() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            xs: int[] := []
            xs.remove(0)
            """);

        Assert.NotEqual(0, exitCode);
        Assert.Equal("", stdout);
        Assert.Contains(ErrorCatalog.E5101.Code, stderr);
    }

    [Fact]
    public void Insert_OutOfRange_CaughtByTryCatch() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            try {
                xs := [1, 2]
                xs.insert(-1, 9)
            } catch (e: IndexError) {
                print("caught")
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("caught" + NL, stdout);
    }

    [Fact]
    public void Remove_OutOfRange_CaughtByTryCatch() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            try {
                xs := [1, 2]
                xs.remove(2)
            } catch (e: IndexError) {
                print("caught")
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("caught" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // readonly rejection at compile time (E0204) — for all four members.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("readonly xs := [1, 2]\nxs.append(3)\n")]
    [InlineData("readonly xs := [1, 2]\nxs.insert(0, 3)\n")]
    [InlineData("readonly xs := [1, 2]\nxs.remove(0)\n")]
    [InlineData("readonly xs := [1, 2]\nxs.clear()\n")]
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
    public void Append_ThroughSecondBinding_VisibleThroughFirst() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := [1, 2]
            b := a
            b.append(3)
            print(a.length)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("3" + NL, stdout);
    }

    [Fact]
    public void Append_ThroughFunctionParameter_VisibleToCallerAfterReturn() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            fn addOne(xs: int[]): void {
                xs.append(1)
            }
            a := [1, 2]
            addOne(a)
            print(a.length)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal("", stderr);
        Assert.Equal("3" + NL, stdout);
    }
}
