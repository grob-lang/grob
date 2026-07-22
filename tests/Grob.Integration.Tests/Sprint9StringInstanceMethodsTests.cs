using System.Text;

using Grob.Cli;
using Grob.Core;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 — primitive instance-method dispatch (D-066), proven end to end through the
/// full pipeline on <c>string</c>. Closes the release-gate blocker: the validation
/// scripts' <c>.split</c>/<c>.replace</c>/<c>.contains</c> calls, previously unreachable
/// (no compiler-side dispatch existed for any primitive receiver — D-362), now compile
/// and run.
/// </summary>
public sealed class Sprint9StringInstanceMethodsTests {
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

    // -----------------------------------------------------------------------
    // The exact release-gate shapes the validation scripts use.
    // -----------------------------------------------------------------------

    [Fact]
    public void Split_ByPipe_PrintsEachPart() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            for part in "a|b|c".split("|") {
                print(part)
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"a{Environment.NewLine}b{Environment.NewLine}c{Environment.NewLine}", stdout);
    }

    [Fact]
    public void Replace_ReplacesEveryOccurrence() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("x".replace("x", "y"))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("y" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Contains_FindsSubstring() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("abc".contains("b"))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("true" + Environment.NewLine, stdout);
    }

    // -----------------------------------------------------------------------
    // Property access and a chained line-processing shape (the scripts doc's
    // "fluent string methods" headline).
    // -----------------------------------------------------------------------

    [Fact]
    public void Length_And_TrimUpperChain_ProduceExpectedOutput() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            s := "  hello  "
            print(s.length)
            print(s.trim().upper())
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"9{Environment.NewLine}HELLO{Environment.NewLine}", stdout);
    }

    // -----------------------------------------------------------------------
    // Nullable toInt()/toFloat() and the throwing IndexError seam, end to end.
    // -----------------------------------------------------------------------

    [Fact]
    public void ToInt_Unparseable_NullCoalescesToFallback() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("nope".toInt() ?? -1)""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("-1" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Substring_OutOfRange_UncaughtIndexError_ExitsNonZero() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("hi".substring(0, 10))""" + "\n");

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains(ErrorCatalog.E5101.Code, stderr);
    }

    [Fact]
    public void Substring_OutOfRange_CaughtByTryCatch() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            try {
                print("hi".substring(0, 10))
            } catch (e: IndexError) {
                print("caught")
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("caught" + Environment.NewLine, stdout);
    }

    // -----------------------------------------------------------------------
    // repeat's native-seam allocation ceiling (D-366): a count that does not
    // overflow the checked(...) cast but would still ask the CLR to allocate an
    // unreasonable buffer must raise a catchable GrobError end to end, not an
    // uncoded host exception.
    // -----------------------------------------------------------------------

    [Fact]
    public void Repeat_ExceedsAllocationCeiling_UncaughtIndexError_ExitsNonZero() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("a".repeat(500000000))""" + "\n");

        Assert.NotEqual(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains(ErrorCatalog.E5101.Code, stderr);
    }

    [Fact]
    public void Repeat_ExceedsAllocationCeiling_CaughtByTryCatch() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            try {
                print("a".repeat(500000000))
            } catch (e: IndexError) {
                print("caught")
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("caught" + Environment.NewLine, stdout);
    }

    // -----------------------------------------------------------------------
    // padLeft/padRight/truncate (D-365) — end to end with and without the
    // optional trailing argument, proving the compiler's default-argument
    // fill against the real runtime natives.
    // -----------------------------------------------------------------------

    [Fact]
    public void PadLeft_WithoutOptionalChar_PadsWithSpace() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("7".padLeft(3))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("  7" + Environment.NewLine, stdout);
    }

    [Fact]
    public void PadLeft_WithOptionalChar_PadsWithSuppliedChar() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("7".padLeft(3, "0"))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("007" + Environment.NewLine, stdout);
    }

    [Fact]
    public void PadRight_WithoutOptionalChar_PadsWithSpace() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("7".padRight(3))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("7  " + Environment.NewLine, stdout);
    }

    [Fact]
    public void PadRight_WithOptionalChar_PadsWithSuppliedChar() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("7".padRight(3, "0"))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("700" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Truncate_WithoutOptionalSuffix_UsesDefaultEllipsis() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("hello world".truncate(8))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("hello..." + Environment.NewLine, stdout);
    }

    [Fact]
    public void Truncate_WithOptionalSuffix_UsesSuppliedSuffix() {
        (string stdout, string stderr, int exitCode) = RunSource("""print("hello world".truncate(8, "-"))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("hello w-" + Environment.NewLine, stdout);
    }
}
