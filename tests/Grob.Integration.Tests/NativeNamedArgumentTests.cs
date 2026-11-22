using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Named arguments to natives, end to end (D-446, D-447; native named arguments change
/// group 2): namespace natives and <c>input</c> bind by name and run with the bound
/// arguments in the right slots, and an <c>int</c> argument bound to a <c>float</c>
/// parameter is widened before the call (R-58, Chris's ruling of 22 November).
/// </summary>
public sealed class NativeNamedArgumentTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source, string stdinText = "") {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr, new StringReader(stdinText), verbose: false).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    private static string[] Lines(string stdout) =>
        stdout.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    private static void AssertRuns(string source, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, Lines(stdout));
    }

    [Fact]
    public void RegexCompile_NamedTimeout_SkipsAndFillsFlags() =>
        AssertRuns("""
            r := regex.compile("a+", timeout: 2)
            print("[${r.flags}] ${r.timeout}")
            print(r.isMatch("caat"))
            """, "[] 2", "true");

    [Fact]
    public void RegexCompile_NamedInEitherOrder_BindsTheSame() =>
        AssertRuns("""
            a := regex.compile("A", flags: "i", timeout: 3)
            b := regex.compile("A", timeout: 3, flags: "i")
            print("${a.flags} ${a.timeout} ${a.isMatch("a")}")
            print("${b.flags} ${b.timeout} ${b.isMatch("a")}")
            """, "i 3 true", "i 3 true");

    [Fact]
    public void DateParse_NamedPattern_ParsesWithThePattern() =>
        AssertRuns("""
            d := date.parse("05/04/2026", pattern: "dd/MM/yyyy")
            print(d.month)
            print(d.day)
            """, "4", "5");

    [Fact]
    public void Input_NamedPrompt_PromptsAndReads() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            name := input(prompt: "Name: ")
            print("hi ${name}")
            """, "Ada\n");

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal("Name: hi Ada", stdout.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n'));
    }

    // -----------------------------------------------------------------------
    // Groups 3 and 5: primitive methods bind; sort binds descending; the rest refuse.
    // -----------------------------------------------------------------------

    [Fact]
    public void PadLeft_NamedChar_PadsWithIt() =>
        AssertRuns("""
            s := "abc"
            print(s.padLeft(5, char: "*"))
            print(s.padLeft(5))
            print("[${s.padRight(5, char: ".")}]")
            """, "**abc", "  abc", "[abc..]");

    [Fact]
    public void ArraySort_NamedDescending_SortsDescending() =>
        AssertRuns("""
            xs := [2, 3, 1]
            print(xs.sort(x => x, descending: true))
            print(xs.sort(x => x))
            print(xs.sort(x => x, descending: false))
            """, "[3, 2, 1]", "[1, 2, 3]", "[1, 2, 3]");

    [Theory]
    [InlineData("xs := [1, 2, 3]\nxs.insert(value: 9, index: 0)\n", "'insert' does not accept named arguments")]
    [InlineData("m := map<string, int>{\"a\": 1}\nm.set(value: 2, key: \"b\")\n", "'set' does not accept named arguments")]
    [InlineData("print(value: \"x\")\n", "'print' does not accept named arguments")]
    [InlineData("exit(code: 3)\n", "'exit' does not accept named arguments")]
    [InlineData("lam := x => x.insert(value: 9, index: 0)\n", "'insert' does not accept named arguments")]
    public void CallWithNoSignature_NamedArgument_FailsToCompileWithE0011(string source, string message) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stdout);
        Assert.NotEqual(0, exitCode);
        Assert.Contains("E0011", stderr, StringComparison.Ordinal);
        Assert.Contains(message, stderr, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // R-58 native widening.
    // -----------------------------------------------------------------------

    [Fact]
    public void MathSqrt_IntArgument_RunsWidened() =>
        AssertRuns("""
            print(math.sqrt(4) == 2.0)
            print(math.pow(2, 10) == 1024.0)
            """, "true", "true");

    [Fact]
    public void ScriptCall_IntArgumentToFloatParameter_RunsWidened() =>
        AssertRuns("""
            fn half(v: float, by: float = 2.0): float {
                return v / by
            }
            print(half(3) == 1.5)
            print(half(9, by: 3) == 3.0)
            """, "true", "true");

    // -----------------------------------------------------------------------
    // Evaluation order (OQ-028).
    // -----------------------------------------------------------------------

    /// <remarks>
    /// OQ-028 (evaluation order of reordered named arguments) is open. Today a script
    /// function's arguments are evaluated in parameter order, not the order written, and
    /// a native given the same reordered call matches it. This test pins that the two
    /// agree; whoever resolves OQ-028 changes both together and updates this test.
    /// </remarks>
    [Fact]
    public void ReorderedCall_ScriptFunctionAndNative_EvaluateArgumentsInTheSameOrder() =>
        AssertRuns("""
            fn trace(label: string, value: string): string {
                print(label)
                return value
            }
            fn traceInt(label: string, value: int): int {
                print(label)
                return value
            }
            fn compileLike(pattern: string, flags: string = "", timeout: int = 5): string {
                return pattern
            }
            print("script")
            s := compileLike(trace("pattern", "a"), timeout: traceInt("timeout", 2), flags: trace("flags", "i"))
            print("native")
            r := regex.compile(trace("pattern", "a"), timeout: traceInt("timeout", 2), flags: trace("flags", "i"))
            """, "script", "pattern", "flags", "timeout", "native", "pattern", "flags", "timeout");
}
