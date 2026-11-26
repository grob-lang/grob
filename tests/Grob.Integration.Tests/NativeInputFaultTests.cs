using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// A native given input it cannot act on raises a Grob runtime error a script can catch, not a
/// host exception out of .NET (R-69, D-459): <c>toZone</c> with an unknown zone, <c>replace</c>
/// with an empty search text and <c>guid.newV5</c> with a namespace that is not a guid. Each is a
/// <c>ParseError</c> or <c>RuntimeError</c> with its registered code, reported at its line when
/// uncaught, runs the <c>finally</c> a script puts around it once, and does the same from a
/// callback.
/// </summary>
public sealed class NativeInputFaultTests {
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

    private static void AssertRunsToEnd(string source, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, Lines(stdout));
    }

    /// <summary>
    /// The program prints <paramref name="printed"/> and then fails with <paramref name="header"/>
    /// at <paramref name="line"/>; a runtime diagnostic carries <c>file:line</c> only (D-322).
    /// </summary>
    private static void AssertFailsAt(string source, string header, int line, params string[] printed) {
        (string stdout, string stderr, int exitCode) = RunSource(source);
        string[] errorLines = Lines(stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal(header, errorLines[0]);
        Assert.StartsWith("  --> ", errorLines[1], StringComparison.Ordinal);
        Assert.EndsWith($".grob:{line}", errorLines[1], StringComparison.Ordinal);
        Assert.Equal(2, errorLines.Length);
        Assert.Equal(printed, stdout.Length == 0 ? Array.Empty<string>() : Lines(stdout));
    }

    // -----------------------------------------------------------------------
    // toZone
    // -----------------------------------------------------------------------

    private const string UnknownZoneMessage = "date.toZone: 'abc' is not a known time zone.";

    [Fact]
    public void ToZone_UnknownZone_CaughtAsParseError() =>
        AssertRunsToEnd("""
            try {
                print(date.now().toZone("abc").toIso())
            } catch (e: ParseError) {
                print("caught ${e.message}")
            }
            print("after")
            """, $"caught {UnknownZoneMessage}", "after");

    [Fact]
    public void ToZone_UnknownZone_UncaughtIsE5702AtTheCall() =>
        AssertFailsAt("""
            d := date.now()
            print("before")
            print(d.toZone("abc").toIso())
            """, $"error[E5702]: {UnknownZoneMessage}", 3, "before");

    [Fact]
    public void ToZone_UnknownZone_RunsAFinallyOnceBeforeReportingIt() =>
        AssertFailsAt("""
            try {
                print(date.now().toZone("abc").toIso())
            } finally {
                print("done")
            }
            """, $"error[E5702]: {UnknownZoneMessage}", 2, "done");

    [Fact]
    public void ToZone_UnknownZoneInACallback_CaughtByTheScript() =>
        AssertRunsToEnd("""
            keep := "kept"
            try {
                [1, 2].each(x => print(date.now().toZone("abc").toIso()))
            } catch (e: ParseError) {
                print("caught")
            }
            print(keep)
            """, "caught", "kept");

    [Fact]
    public void ToZone_KnownZone_StillConverts() =>
        AssertRunsToEnd("""
            print(date.parse("2026-04-05T12:00:00Z").toZone("UTC").hour)
            """, "12");

    // -----------------------------------------------------------------------
    // replace
    // -----------------------------------------------------------------------

    private const string EmptyReplaceMessage = "replace: the text to replace must not be empty.";

    [Fact]
    public void Replace_EmptySearchText_CaughtAsRuntimeError() =>
        AssertRunsToEnd("""
            try {
                print("a".replace("", "b"))
            } catch (e: RuntimeError) {
                print("caught ${e.message}")
            }
            print("after")
            """, $"caught {EmptyReplaceMessage}", "after");

    [Fact]
    public void Replace_EmptySearchText_UncaughtIsE5903AtTheCall() =>
        AssertFailsAt("""
            print("before")
            print("a".replace("", "b"))
            """, $"error[E5903]: {EmptyReplaceMessage}", 2, "before");

    [Fact]
    public void Replace_EmptySearchText_RunsAFinallyOnceBeforeReportingIt() =>
        AssertFailsAt("""
            try {
                print("a".replace("", "b"))
            } finally {
                print("done")
            }
            """, $"error[E5903]: {EmptyReplaceMessage}", 2, "done");

    [Fact]
    public void Replace_EmptySearchTextFromAVariable_CaughtAsRuntimeError() =>
        AssertRunsToEnd("""
            needle := ""
            try {
                print("abc".replace(needle, "-"))
            } catch (e: RuntimeError) {
                print("caught")
            }
            """, "caught");

    [Fact]
    public void Replace_EmptySearchTextInACallback_CaughtByTheScript() =>
        AssertRunsToEnd("""
            try {
                print([1, 2].select(n => "a".replace("", "-")))
            } catch (e: RuntimeError) {
                print("caught")
            }
            print("after")
            """, "caught", "after");

    [Fact]
    public void Replace_NonEmptySearchText_StillReplaces() =>
        AssertRunsToEnd("""
            print("a-a-a".replace("a", "z"))
            """, "z-z-z");

    // -----------------------------------------------------------------------
    // guid.newV5
    // -----------------------------------------------------------------------

    // An anonymous struct's field has no type the checker knows (R-57), so a date routed through
    // one reaches newV5's namespace parameter, which the checker would otherwise refuse (E0004).
    private const string RoutedDate = "c := #{ v: date.now() }.v";

    private const string NotAGuidMessage = "guid.newV5: the namespace must be a guid, not a struct of type 'date'.";

    [Fact]
    public void NewV5_NamespaceThatIsNotAGuid_CaughtAsRuntimeError() =>
        AssertRunsToEnd($$"""
            {{RoutedDate}}
            try {
                print(guid.newV5(c, "a"))
            } catch (e: RuntimeError) {
                print("caught ${e.message}")
            }
            print("after")
            """, $"caught {NotAGuidMessage}", "after");

    [Fact]
    public void NewV5_NamespaceThatIsNotAGuid_UncaughtIsE5903AtTheCall() =>
        AssertFailsAt($$"""
            {{RoutedDate}}
            print("before")
            print(guid.newV5(c, "a"))
            """, $"error[E5903]: {NotAGuidMessage}", 3, "before");

    [Fact]
    public void NewV5_GuidNamespace_StillWorks() =>
        AssertRunsToEnd("""
            print(guid.newV5(guid.namespaces.dns, "a").version)
            """, "5");
}
