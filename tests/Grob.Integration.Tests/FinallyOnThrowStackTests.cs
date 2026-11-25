using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// A <c>finally</c> that a throw runs finds the stack as the normal path leaves it (R-79,
/// D-455), whatever the throw left above the slot the <c>try</c> began at: a local the
/// <c>try</c> body declared, a catch binding and a local the catch body declared, a callee's
/// frame or the operands a failing operation had pushed. Each case is one of R-79's
/// reproducers or a sibling shape, run through the CLI.
/// </summary>
public sealed class FinallyOnThrowStackTests {
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

    private static void AssertRuns(string source, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, Lines(stdout));
    }

    /// <summary>
    /// Asserts the program prints <paramref name="expected"/>, then fails with
    /// <paramref name="expectedCode"/> located at the original failure's line, not at a line
    /// of the finally that ran after it. A runtime diagnostic carries <c>file:line</c> only
    /// (D-322), so the location ends at the line, with no column after it.
    /// </summary>
    private static void AssertFailsAfterPrinting(
            string source, string expectedCode, int expectedLine, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);
        string[] errorLines = Lines(stderr);

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"error[{expectedCode}]", errorLines[0], StringComparison.Ordinal);
        Assert.StartsWith("  --> ", errorLines[1], StringComparison.Ordinal);
        Assert.EndsWith($".grob:{expectedLine}", errorLines[1], StringComparison.Ordinal);
        Assert.Equal(expected, Lines(stdout));
    }

    [Fact]
    public void Throw_PastALocalInTheTryBody_FinallyReadsItsOwnLocal() =>
        AssertFailsAfterPrinting("""
            try {
                a := true
                throw RuntimeError { message: "x" }
            } finally {
                b := 1
                print(b + 1)
            }
            """, "E5904", 3, "2");

    [Fact]
    public void Throw_FromACatchBodyWithALocal_FinallyReadsItsOwnLocal() =>
        AssertRuns("""
            try {
                try {
                    throw RuntimeError { message: "x" }
                } catch e {
                    c := 1
                    throw RuntimeError { message: "y" }
                } finally {
                    b := "s"
                    print(b + "!")
                }
            } catch z {
                print(z.message)
            }
            """, "s!", "y");

    [Fact]
    public void Throw_FromACalledFunction_FinallyReadsItsOwnLocal() =>
        AssertFailsAfterPrinting("""
            fn f(): int {
                c := 7
                throw RuntimeError { message: "x" }
            }
            try {
                f()
            } finally {
                b := 1
                print(b + 1)
            }
            """, "E5904", 3, "2");

    [Fact]
    public void RuntimeErrorInTheTryBody_FinallyReadsItsOwnLocal() =>
        AssertFailsAfterPrinting("""
            try {
                a := 1
                print(10 / 0)
            } finally {
                b := "s"
                print(b + "!")
            }
            """, "E5002", 3, "s!");

    [Fact]
    public void ErrorANativeRaisesInTheTryBody_FinallyReadsItsOwnLocal() =>
        AssertFailsAfterPrinting("""
            try {
                a := 1
                print(date.parse("nope"))
            } finally {
                b := "s"
                print(b + "!")
            }
            """, "E5702", 3, "s!");

    [Fact]
    public void NestedTry_EachFinallyReadsItsOwnLocalsAndTheOnesBelow() =>
        AssertRuns("""
            try {
                x := 10
                try {
                    y := 20
                    throw RuntimeError { message: "boom" }
                } finally {
                    z := 30
                    print(x + z)
                }
            } catch e {
                w := e.message
                print(w)
            } finally {
                v := "done"
                print(v)
            }
            """, "40", "boom", "done");

    [Fact]
    public void Throw_InAFunctionWithLocalsBelowTheTry_FinallyLeavesThemIntact() =>
        AssertRuns("""
            fn g(p: int): int {
                q := p * 2
                try {
                    r := "noise"
                    throw RuntimeError { message: "x" }
                } catch e {
                    s := 0
                } finally {
                    t := 100
                    print(p + q + t)
                }
                return q
            }
            print(g(1))
            """, "103", "2");
}
