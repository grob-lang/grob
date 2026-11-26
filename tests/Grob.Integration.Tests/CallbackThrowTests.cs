using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// An error raised in a callback that a native runs (R-91, D-458) is caught by the
/// script's <c>try</c> and the script runs to its end; one the script does not catch is
/// reported with its own code at the line that raised it. The error leaves the native: its
/// loop goes no further, a <c>finally</c> between the error and the <c>catch</c> runs once
/// in order, and an error caught inside the callback never reaches the script.
/// </summary>
public sealed class CallbackThrowTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr, new StringReader(string.Empty), verbose: false).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    /// <summary>The program runs to its end, exits 0, writes nothing to stderr and prints <paramref name="expected"/>.</summary>
    private static void AssertRunsToEnd(string source, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, Lines(stdout));
    }

    /// <summary>
    /// The program prints <paramref name="expected"/> and then fails with
    /// <paramref name="expectedHeader"/> located at <paramref name="expectedLine"/>. A runtime
    /// diagnostic carries <c>file:line</c> only (D-322), so the location ends at the line.
    /// </summary>
    private static void AssertFailsAfterPrinting(
            string source, string expectedHeader, int expectedLine, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);
        string[] errorLines = Lines(stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal(expectedHeader, errorLines[0]);
        Assert.StartsWith("  --> ", errorLines[1], StringComparison.Ordinal);
        Assert.EndsWith($".grob:{expectedLine}", errorLines[1], StringComparison.Ordinal);
        Assert.Equal(expected, Lines(stdout));
    }

    [Fact]
    public void OverflowInSelectCallback_CaughtByScript_RunsToEnd() =>
        AssertRunsToEnd("""
            try {
                n := [1].select(p6 => p6 + 9223372036854775807)
            } catch err14 {
                print("caught")
            }
            print("after")
            """, "caught", "after");

    [Fact]
    public void DivideByZeroInEachCallback_CaughtByScript_LoopStopsAndLocalSurvives() =>
        AssertRunsToEnd("""
            keep := "kept"
            try {
                [1, 2, 3].each(x => print(x / (x - 1)))
            } catch (e: ArithmeticError) {
                print("caught ${e.message}")
            }
            print(keep)
            """, "caught integer division by zero", "kept");

    [Fact]
    public void DivideByZeroInFilterCallback_CaughtByScript_RunsToEnd() =>
        AssertRunsToEnd("""
            try {
                big := [1, 2].filter(x => x / 0 > 0)
            } catch (e: ArithmeticError) {
                print("caught")
            }
            print("after")
            """, "caught", "after");

    [Fact]
    public void DivideByZeroInSortCallback_CaughtByScript_RunsToEnd() =>
        AssertRunsToEnd("""
            try {
                ordered := [2, 1].sort(x => x / 0)
            } catch (e: ArithmeticError) {
                print("caught")
            }
            print("after")
            """, "caught", "after");

    [Fact]
    public void ThrowInFunctionTheCallbackCalls_CaughtByScript_ErrorKeepsItsFields() =>
        AssertRunsToEnd("""
            fn fail(): int {
                throw RuntimeError { message: "boom" }
            }
            try {
                n := [1, 2].select(x => fail())
            } catch (e: RuntimeError) {
                print("caught ${e.message}")
            }
            print("after")
            """, "caught boom", "after");

    [Fact]
    public void ErrorInNativeRunFromCallback_CaughtByScript_RunsToEnd() =>
        AssertRunsToEnd("""
            try {
                n := [1, 2].select(x => [x].select(y => y / 0).length)
            } catch (e: ArithmeticError) {
                print("caught")
            }
            print("after")
            """, "caught", "after");

    [Fact]
    public void FinallyInCalleeAndAroundCall_EachRunsOnceInOrder_ThenScriptCatches() =>
        AssertRunsToEnd("""
            fn risky(): int {
                try {
                    return 1 / 0
                } finally {
                    print("inner finally")
                }
            }
            try {
                try {
                    n := [1, 2].select(x => risky())
                } finally {
                    print("outer finally")
                }
            } catch (e: ArithmeticError) {
                print("caught")
            }
            print("after")
            """, "inner finally", "outer finally", "caught", "after");

    [Fact]
    public void ErrorCaughtInsideCallback_NeverReachesScript_LoopRunsEveryElement() =>
        AssertRunsToEnd("""
            fn safe(): int {
                try {
                    return 1 / 0
                } catch (e: ArithmeticError) {
                    print("inner")
                }
                return 0
            }
            try {
                [1, 2].each(x => print(safe()))
            } catch e {
                print("wrong")
            }
            print("after")
            """, "inner", "0", "inner", "0", "after");

    [Fact]
    public void CallbackErrorInFinallyThatAThrowRuns_CaughtInsideFinally_FirstErrorStillCaught() =>
        AssertRunsToEnd("""
            try {
                try {
                    throw RuntimeError { message: "first" }
                } finally {
                    try {
                        [1].each(x => print(x / 0))
                    } catch (e: ArithmeticError) {
                        print("caught in finally")
                    }
                }
            } catch (e: RuntimeError) {
                print("caught ${e.message}")
            }
            print("after")
            """, "caught in finally", "caught first", "after");

    [Fact]
    public void CallbackErrorEscapingFinallyThatAThrowRuns_ReplacesTheFirstError() =>
        AssertRunsToEnd("""
            try {
                try {
                    throw RuntimeError { message: "first" }
                } finally {
                    [1].each(x => print(x / 0))
                }
            } catch (e: ArithmeticError) {
                print("replaced: ${e.message}")
            }
            print("after")
            """, "replaced: integer division by zero", "after");

    [Fact]
    public void ThrowInCallback_Uncaught_ReportsE5904AtTheThrow() =>
        AssertFailsAfterPrinting("""
            fn fail(): int {
                print("in fail")
                throw RuntimeError { message: "boom" }
            }
            n := [1, 2].select(x => fail())
            print("after")
            """, "error[E5904]: RuntimeError: boom", 3, "in fail");

    [Fact]
    public void DivideByZeroInCallback_Uncaught_ReportsE5002AtTheCallback() =>
        AssertFailsAfterPrinting("""
            print("before")
            [1, 2].each(x =>
                print(x / 0))
            print("after")
            """, "error[E5002]: integer division by zero", 3, "before");

    [Fact]
    public void ThrowInCallback_ReplacedByScriptFinallyThrow_ReportsTheReplacementAtTheFirstThrow() =>
        AssertFailsAfterPrinting("""
            fn fail(): int {
                throw RuntimeError { message: "first" }
            }
            try {
                n := [1].select(x => fail())
            } finally {
                print("finally")
                throw LookupError { message: "second" }
            }
            print("after")
            """, "error[E5904]: LookupError: second", 2, "finally");

    [Fact]
    public void ThrowInCallback_UncaughtPastScriptFinally_FinallyRunsThenE5904() =>
        AssertFailsAfterPrinting("""
            fn fail(): int {
                throw RuntimeError { message: "boom" }
            }
            try {
                n := [1].select(x => fail())
            } finally {
                print("finally")
            }
            print("after")
            """, "error[E5904]: RuntimeError: boom", 2, "finally");
}
