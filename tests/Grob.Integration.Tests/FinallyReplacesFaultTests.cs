using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// A <c>finally</c> that throws while an error a native or the VM raised is in flight replaces
/// that error (R-99, D-462, D-275), run through the CLI. Uncaught, the replacement is reported as
/// E5904, located at the line the fault was raised on, as an uncaught <c>throw</c> replaced the same
/// way is (D-458); caught further out, the outer <c>catch</c> receives it. A finally that does
/// not throw leaves the fault to surface with its own code. Every row is one route to the handler
/// walk; a nil dereference and a full value stack cannot be reached from source and are covered
/// by the VM tests alone.
/// </summary>
public sealed class FinallyReplacesFaultTests {
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
    /// The definitions every row's fault draws on, five lines long, so a fault inside
    /// <c>risky</c> or <c>deep</c> is on line 1 or 2 and the try body's own line is 7.
    /// </summary>
    private const string Prelude = """
        fn risky(d: int): int { return 10 / d }
        fn deep(n: int): int { return deep(n + 1) }
        zero := 0
        big := 9223372036854775807
        arr := [1, 2, 3]

        """;

    /// <summary>Every route: the try body's statement, the code its fault keeps and the line it is raised on.</summary>
    public static TheoryData<string, string, int> Routes => new() {
        { """print(date.now().toZone("abc").toIso())""", "E5702", 7 },
        { """print("abc".replace("", "x"))""", "E5903", 7 },
        { "print(arr[5])", "E5101", 7 },
        { "print(10 / zero)", "E5002", 7 },
        { "print(big + 1)", "E5001", 7 },
        { "print(deep(0))", "E5901", 2 },
        { "print(risky(zero))", "E5002", 1 },
        { "[1, 2].each(x => print(x / zero))", "E5002", 7 },
    };

    /// <summary>
    /// The prelude, then <c>try { statement } finally { print("finally"); [throw] }</c>, with an
    /// outer <c>try</c> and <c>catch e</c> around it when <paramref name="outerCatch"/> is set.
    /// The try body is on line 7 either way, the outer <c>try</c> sharing line 6 with the inner.
    /// </summary>
    private static string Script(string statement, bool finallyThrows, bool outerCatch) {
        string open = outerCatch ? "try { try {" : "try {";
        string close = outerCatch ? "} } catch e {\n    print(\"caught ${e.message}\")\n}" : "}";
        string replace = finallyThrows ? "    throw RuntimeError { message: \"replaced\" }\n" : string.Empty;
        return $"{Prelude}{open}\n    {statement}\n}} finally {{\n    print(\"finally\")\n{replace}{close}\nprint(\"after\")\n";
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Fault_FinallyThrowsAndNothingCatches_ReportsE5904NamingTheReplacementAtTheFault(
            string statement, string faultCode, int faultLine) {
        _ = faultCode;
        (string stdout, string stderr, int exitCode) = RunSource(Script(statement, finallyThrows: true, outerCatch: false));
        string[] errorLines = Lines(stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal("error[E5904]: RuntimeError: replaced", errorLines[0]);
        Assert.EndsWith($".grob:{faultLine}", errorLines[1], StringComparison.Ordinal);
        Assert.Equal(["finally"], Lines(stdout));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Fault_FinallyThrowsAndAnOuterCatchCatches_TheCatchReceivesTheReplacement(
            string statement, string faultCode, int faultLine) {
        _ = (faultCode, faultLine);
        (string stdout, string stderr, int exitCode) = RunSource(Script(statement, finallyThrows: true, outerCatch: true));

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal(["finally", "caught replaced", "after"], Lines(stdout));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Fault_FinallyDoesNotThrow_TheFaultSurfacesWithItsOwnCodeAndLine(
            string statement, string faultCode, int faultLine) {
        (string stdout, string stderr, int exitCode) = RunSource(Script(statement, finallyThrows: false, outerCatch: false));
        string[] errorLines = Lines(stderr);

        Assert.Equal(1, exitCode);
        Assert.StartsWith($"error[{faultCode}]: ", errorLines[0], StringComparison.Ordinal);
        Assert.EndsWith($".grob:{faultLine}", errorLines[1], StringComparison.Ordinal);
        Assert.Equal(["finally"], Lines(stdout));
    }

    [Fact]
    public void Fault_NestedFinallies_OnlyTheOuterReplaces_ReportsTheOuterReplacement() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            arr := [1, 2, 3]
            try {
                try {
                    print(arr[5])
                } finally {
                    print("inner")
                }
            } finally {
                print("outer")
                throw LookupError { message: "outer replaced" }
            }
            print("after")
            """);
        string[] errorLines = Lines(stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal("error[E5904]: LookupError: outer replaced", errorLines[0]);
        Assert.EndsWith(".grob:4", errorLines[1], StringComparison.Ordinal);
        Assert.Equal(["inner", "outer"], Lines(stdout));
    }

    [Fact]
    public void CallbackFault_ReplacedByAFinallyInTheCallback_Uncaught_ReportsE5904AtTheFault() {
        // The fault is in a callback of a native the outer callback runs; the outer callback's
        // own finally replaces it, and the replacement leaves both natives (D-458).
        (string stdout, string stderr, int exitCode) = RunSource("""
            zero := 0
            [1].each(x => {
                try {
                    [1].each(y => print(y / zero))
                } finally {
                    print("finally")
                    throw RuntimeError { message: "replaced" }
                }
            })
            print("after")
            """);
        string[] errorLines = Lines(stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal("error[E5904]: RuntimeError: replaced", errorLines[0]);
        Assert.EndsWith(".grob:4", errorLines[1], StringComparison.Ordinal);
        Assert.Equal(["finally"], Lines(stdout));
    }

    [Fact]
    public void ScriptThrow_FinallyThrowsAndNothingCatches_ReportsTheReplacementAtTheFirstThrow() {
        // The control: a script throw replaced the same way was already reported so (D-458).
        (string stdout, string stderr, int exitCode) = RunSource("""
            try {
                throw RuntimeError { message: "original" }
            } finally {
                print("finally")
                throw RuntimeError { message: "replaced" }
            }
            """);
        string[] errorLines = Lines(stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal("error[E5904]: RuntimeError: replaced", errorLines[0]);
        Assert.EndsWith(".grob:2", errorLines[1], StringComparison.Ordinal);
        Assert.Equal(["finally"], Lines(stdout));
    }
}
