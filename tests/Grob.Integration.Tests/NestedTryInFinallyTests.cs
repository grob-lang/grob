using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// A <c>finally</c> that a throw runs ends at its own end, however control left a <c>try</c>
/// nested inside it (R-80, D-457): an error a typed <c>catch</c> does not match, caught
/// further out; a <c>break</c> or <c>continue</c> out of a <c>try</c> body; a called
/// function's <c>return</c> from inside its own <c>try</c>; or a nested <c>finally</c> run
/// on the way to a <c>catch</c>. Each ends with the first error, which the <c>finally</c>
/// did not catch, so nothing after the outer <c>try</c> runs.
/// </summary>
public sealed class NestedTryInFinallyTests {
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

    /// <summary>
    /// Asserts the program prints <paramref name="expected"/>, then fails with E5904 for the
    /// first error, <c>RuntimeError: first</c>, located at <paramref name="expectedLine"/>. A
    /// runtime diagnostic carries <c>file:line</c> only (D-322), so the location ends at the
    /// line.
    /// </summary>
    private static void AssertFirstErrorAfterPrinting(string source, int expectedLine, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);
        string[] errorLines = Lines(stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal("error[E5904]: RuntimeError: first", errorLines[0]);
        Assert.StartsWith("  --> ", errorLines[1], StringComparison.Ordinal);
        Assert.EndsWith($".grob:{expectedLine}", errorLines[1], StringComparison.Ordinal);
        Assert.Equal(expected, Lines(stdout));
    }

    [Fact]
    public void TypedCatchMisses_OuterCatchRuns_ThenTheFirstErrorIsReported() =>
        AssertFirstErrorAfterPrinting("""
            try {
                throw RuntimeError { message: "first" }
            } finally {
                try {
                    try {
                        throw LookupError { message: "second" }
                    } catch (x: IoError) {
                        print("wrong")
                    }
                } catch y {
                    print("caught ${y.message}")
                }
            }
            print("after")
            """, 2, "caught second");

    [Fact]
    public void TwoTypedCatchesMiss_OuterCatchRuns_ThenTheFirstErrorIsReported() =>
        AssertFirstErrorAfterPrinting("""
            try {
                throw RuntimeError { message: "first" }
            } finally {
                try {
                    try {
                        try {
                            throw LookupError { message: "second" }
                        } catch (x: IoError) {
                            print("wrong")
                        }
                    } catch (z: IoError) {
                        print("wrong")
                    }
                } catch y {
                    print("caught ${y.message}")
                }
            }
            print("after")
            """, 2, "caught second");

    [Fact]
    public void ErrorCaughtWhereItIsThrown_ThenTheFirstErrorIsReported() =>
        AssertFirstErrorAfterPrinting("""
            try {
                throw RuntimeError { message: "first" }
            } finally {
                try {
                    throw LookupError { message: "second" }
                } catch (x: LookupError) {
                    print("caught ${x.message}")
                }
            }
            print("after")
            """, 2, "caught second");

    [Fact]
    public void BreakOutOfATryInALoop_FinallyRunsOn_ThenTheFirstErrorIsReported() =>
        AssertFirstErrorAfterPrinting("""
            try {
                throw RuntimeError { message: "first" }
            } finally {
                for i in 1..3 {
                    try {
                        break
                    } catch e {
                        print("wrong")
                    }
                }
                print("finally done")
            }
            print("after")
            """, 2, "finally done");

    [Fact]
    public void ContinueOutOfATryInALoop_FinallyRunsOn_ThenTheFirstErrorIsReported() =>
        AssertFirstErrorAfterPrinting("""
            try {
                throw RuntimeError { message: "first" }
            } finally {
                for i in 1..3 {
                    try {
                        if (i == 2) {
                            continue
                        }
                    } catch e {
                        print("wrong")
                    }
                    print(i)
                }
            }
            print("after")
            """, 2, "1", "3");

    [Fact]
    public void FunctionReturnsFromInsideItsTry_ThenTheFirstErrorIsReported() =>
        AssertFirstErrorAfterPrinting("""
            fn g(): int {
                try {
                    return 1
                } catch e {
                    return 2
                }
            }
            try {
                throw RuntimeError { message: "first" }
            } finally {
                print("g ${g()}")
            }
            print("after")
            """, 9, "g 1");

    [Fact]
    public void NestedFinallyRunsOnTheWayToAnOuterCatch_ThenTheFirstErrorIsReported() =>
        AssertFirstErrorAfterPrinting("""
            try {
                throw RuntimeError { message: "first" }
            } finally {
                try {
                    try {
                        throw LookupError { message: "second" }
                    } finally {
                        print("inner finally")
                    }
                } catch y {
                    print("caught ${y.message}")
                }
            }
            print("after")
            """, 2, "inner finally", "caught second");
}
