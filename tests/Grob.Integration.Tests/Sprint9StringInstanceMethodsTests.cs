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
        (string stdout, _, int exitCode) = RunSource("""print("x".replace("x", "y"))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal("y" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Contains_FindsSubstring() {
        (string stdout, _, int exitCode) = RunSource("""print("abc".contains("b"))""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal("true" + Environment.NewLine, stdout);
    }

    // -----------------------------------------------------------------------
    // Property access and a chained line-processing shape (the scripts doc's
    // "fluent string methods" headline).
    // -----------------------------------------------------------------------

    [Fact]
    public void Length_And_TrimUpperChain_ProduceExpectedOutput() {
        (string stdout, _, int exitCode) = RunSource("""
            s := "  hello  "
            print(s.length)
            print(s.trim().upper())
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal($"9{Environment.NewLine}HELLO{Environment.NewLine}", stdout);
    }

    // -----------------------------------------------------------------------
    // Nullable toInt()/toFloat() and the throwing IndexError seam, end to end.
    // -----------------------------------------------------------------------

    [Fact]
    public void ToInt_Unparseable_NullCoalescesToFallback() {
        (string stdout, _, int exitCode) = RunSource("""print("nope".toInt() ?? -1)""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal("-1" + Environment.NewLine, stdout);
    }

    [Fact]
    public void Substring_OutOfRange_UncaughtIndexError_ExitsNonZero() {
        (_, string stderr, int exitCode) = RunSource("""print("hi".substring(0, 10))""" + "\n");

        Assert.NotEqual(0, exitCode);
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
}
