using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// The <c>regex</c> increment end to end (D-432, D-440), through the full pipeline: a
/// literal and <c>regex.compile</c>, every <c>Regex</c> method, <c>Match</c> and its named
/// groups, the one-shot functions, display, the compile-time E0602, the run-time
/// <c>ParseError</c> (E5702) and the timeout's <c>RuntimeError</c> (E5903).
/// </summary>
public sealed class Sprint9CRegexTests {
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

    private static string[] Lines(string stdout) =>
        stdout.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    private static void AssertRuns(string source, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, Lines(stdout));
    }

    // -----------------------------------------------------------------------
    // The literal (R-30 closed) and regex.compile
    // -----------------------------------------------------------------------

    [Fact]
    public void Literal_MatchesAndPrintsAsALiteral() =>
        AssertRuns("""
            r := /\d+/i
            print(r)
            print(r.isMatch("abc 123"))
            print(r.isMatch("abc"))
            """, @"/\d+/i", "true", "false");

    [Fact]
    public void Literal_InALoop_Matches() =>
        AssertRuns("""
            hits := 0
            for line in ["ERROR a", "info b", "error c"] {
                if (/^error/i.isMatch(line)) {
                    hits++
                }
            }
            print(hits)
            """, "2");

    [Fact]
    public void Compile_CarriesNormalisedFlagsAndItsTimeout() =>
        AssertRuns("""
            r := regex.compile("^a", "mi", 30)
            print(r.pattern)
            print(r.flags)
            print(r.timeout)
            print(r.isMatch("x\nA"))
            print(/a/.timeout)
            """, "^a", "im", "30", "true", "5");

    [Fact]
    public void Compile_BuildsFromARunTimeString() =>
        AssertRuns("""
            prefix := "v1.2"
            r := regex.compile("^" + regex.escape(prefix) + `-\d+$`)
            print(r.isMatch("v1.2-7"))
            print(r.isMatch("v1x2-7"))
            """, "true", "false");

    // -----------------------------------------------------------------------
    // Match and the methods
    // -----------------------------------------------------------------------

    [Fact]
    public void MatchAll_NamedGroups_FilterALog() =>
        AssertRuns("""
            levels := /\b(?<level>ERROR|WARN)\b:\s*(?<text>.*)/
            text := "INFO: ok\nWARN: disk\nERROR: down"
            for m in levels.matchAll(text) {
                print("${m.group("level") ?? "?"} at ${m.index}: ${m.groups[2]}")
            }
            """, "WARN at 9: disk", "ERROR at 20: down");

    [Fact]
    public void Match_ReturnsNilOrTheFirstMatch() =>
        AssertRuns("""
            first := /\d+/.match("a 12 b 34")
            if (first != nil) {
                print(first)
                print(first.length)
            }
            print(/\d+/.match("none") == nil)
            print(/(?<x>a)|(?<y>b)/.match("b")?.group("x") ?? "nil")
            """, "12", "2", "true", "nil");

    [Fact]
    public void Replace_UsesDotNetSyntaxFromABacktickString() =>
        AssertRuns("""
            named := /(?<user>\w+)@(?<host>\w+)/
            print(named.replace("ann@home bob@work", `${host}:${user}`))
            print(/(\w+)@(\w+)/.replaceAll("ann@home bob@work", `$2:$1`))
            print(/a/.replaceAll("banana", `$$`))
            """, "home:ann bob@work", "home:ann work:bob", "b$n$n$");

    // -----------------------------------------------------------------------
    // The function form of replace and replaceAll (D-443, R-44)
    // -----------------------------------------------------------------------

    [Fact]
    public void ReplaceFunction_ReplacesTheFirstMatch() =>
        AssertRuns("""
            print(/\b\w/.replace("hello world", m => m.value.upper()))
            """, "Hello world");

    [Fact]
    public void ReplaceAllFunction_ComputesEachReplacementFromTheMatch() =>
        AssertRuns("""
            print(/(\w+)@(\w+)/.replaceAll("ann@home bob@work", m => m.groups[2] + ":" + m.groups[1]))
            print(/\w/.replaceAll("abc", m => "${m.index}"))
            """, "home:ann work:bob", "012");

    [Fact]
    public void ReplaceAllFunction_BlockBodyCapturesItsScope() =>
        AssertRuns("""
            levels := /\b(ERROR|WARN)\b/
            prefix := "!"
            print(levels.replaceAll("WARN then ERROR", m => {
                lower := m.value.lower()
                "${prefix}${lower}"
            }))
            """, "!warn then !error");

    [Fact]
    public void ReplaceFunction_NamedFunction_IsCalled() =>
        AssertRuns("""
            fn shout(m: Match): string {
                return m.value.upper()
            }
            print(/a/.replaceAll("banana", shout))
            """, "bAnAnA");

    [Fact]
    public void ReplaceFunction_StringForm_IsUnchanged() =>
        AssertRuns("""
            print(/a/.replace("banana", "o"))
            """, "bonana");

    [Fact]
    public void ReplaceFunction_ThrowInsideTheFunction_Propagates() {
        // Uncaught on purpose: catching a throw raised inside a native's callback fails on
        // main today for array select too, and is fixed on its own branch.
        (string stdout, string stderr, int exitCode) = RunSource("""
            x := /a/.replace("banana", m => {
                if (m.index == 1) {
                    throw RuntimeError { message: "stop at ${m.index}" }
                }
                m.value
            })
            print(x)
            """);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5904", stderr);
        Assert.Contains("stop at 1", stderr);
    }

    [Fact]
    public void ReplaceFunction_TimesOutAsARuntimeError() =>
        AssertRuns("""
            r := regex.compile(`^(a+)+$`, "", 1)
            evil := "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!"
            try {
                r.replace(evil, m => m.value)
            } catch (e: RuntimeError) {
                print(e.message)
            }
            """, "Regex.replace: matching exceeded the 1-second limit.");

    [Fact]
    public void ReplaceFunction_NonStringBody_IsE0004BeforeAnythingRuns() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            print("never")
            /a/.replace("a", m => m.index)
            """);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0004", stderr);
        Assert.Contains(":2:18", stderr);
    }

    [Fact]
    public void Split_KeepsCapturedSeparators() =>
        AssertRuns("""
            print(/\s*,\s*/.split("a , b,c").length)
            print(/(,)/.split("a,b").length)
            """, "3", "3");

    // -----------------------------------------------------------------------
    // The one-shot functions
    // -----------------------------------------------------------------------

    [Fact]
    public void OneShotFunctions_TakeStringPatterns() =>
        AssertRuns("""
            print(regex.isMatch(`^\d+$`, "123"))
            print(regex.match(`\d`, "a1")?.value ?? "nil")
            print(regex.matchAll(`\d`, "1 2 3").length)
            print(regex.replace(`\d`, "1 2", "#"))
            print(regex.replaceAll(`\d`, "1 2", "#"))
            print(regex.split(`-`, "a-b-c").length)
            print(regex.escape("a.b"))
            """, "true", "1", "3", "# 2", "# #", "3", @"a\.b");

    // -----------------------------------------------------------------------
    // Errors: compile time, run time and the timeout
    // -----------------------------------------------------------------------

    [Fact]
    public void InvalidLiteral_IsE0602BeforeAnythingRuns() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            print("never")
            r := /a(/
            """);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0602", stderr);
        Assert.Contains(":2:9", stderr);
    }

    [Fact]
    public void InvalidRunTimePattern_IsACatchableParseError() =>
        AssertRuns("""
            pattern := "secret("
            try {
                regex.compile(pattern)
            } catch (e: ParseError) {
                print(e.message.contains("secret"))
                print(e.message.contains("offset 7"))
            }
            """, "false", "true");

    [Fact]
    public void InvalidRunTimePattern_Unhandled_ExitsOneWithE5702() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            pattern := "a("
            regex.isMatch(pattern, "a")
            """);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5702", stderr);
    }

    [Fact]
    public void InvalidTimeout_IsAParseError() =>
        AssertRuns("""
            try {
                regex.compile("a", "", -1)
            } catch (e: ParseError) {
                print("rejected")
            }
            """, "rejected");

    [Fact]
    public void CatastrophicBacktracking_TimesOutAsARuntimeError() =>
        AssertRuns("""
            r := regex.compile(`^(a+)+$`, "", 1)
            evil := "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!"
            try {
                r.isMatch(evil)
            } catch (e: RuntimeError) {
                print(e.message)
            }
            """, "Regex.isMatch: matching exceeded the 1-second limit.");

    [Fact]
    public void RegexProperty_CannotBeAssigned() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            r := /a/
            r.pattern = "b"
            """);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0204", stderr);
        Assert.DoesNotContain("internal error", stderr);
    }
}
