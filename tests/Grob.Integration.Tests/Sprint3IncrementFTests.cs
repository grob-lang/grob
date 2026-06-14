using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 3 Increment F integration tests: the REPL (<see cref="ReplCommand"/>)
/// and the <c>grob run hello.grob</c> Sprint 3 close-gate.
/// </summary>
public sealed class Sprint3IncrementFTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) {
        if (Path.IsPathRooted(name))
            throw new ArgumentException($"Fixture name must be a relative path, got: {name}", nameof(name));
        return Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-3f", name);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    /// <summary>
    /// Drives the REPL with <paramref name="input"/> as scripted user input
    /// and returns the captured stdout and stderr.
    /// </summary>
    private static (string Stdout, string Stderr, int ExitCode) RunRepl(string input) {
        var stdin = new StringReader(input);
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new ReplCommand(stdin, stdout, stderr).Run();
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    // -----------------------------------------------------------------------
    // Sprint 3 close-gate — grob run hello.grob
    // -----------------------------------------------------------------------

    /// <summary>
    /// Close-gate for Sprint 3: <c>grob run hello.grob</c> must produce the
    /// exact three-line output that exercises the full Sprint 3 language surface
    /// (declarations, const, readonly, scope chain, <c>??</c> nil coalescing
    /// and string interpolation).
    /// </summary>
    [Fact]
    public void HelloGrob_RunFile_PrintsExpectedOutputAndExitsZero() {
        (string stdout, string stderr, int exitCode) = RunFile("hello.grob");

        string expected =
            $"Hello, World! Score: 53, version 1.0.{NL}" +
            $"User: anonymous{NL}" +
            $"200{NL}";

        Assert.Equal(0, exitCode);
        Assert.Equal(expected, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // REPL — banner and prompt
    // -----------------------------------------------------------------------

    [Fact]
    public void Repl_EmptyInput_PrintsBannerAndExitsZero() {
        // EOF immediately after the banner is shown.
        (string stdout, string _, int exitCode) = RunRepl(string.Empty);

        Assert.Equal(0, exitCode);
        Assert.Contains("Grob 1.0.0", stdout);
        Assert.Contains("exit", stdout);
    }

    [Fact]
    public void Repl_ExitCommand_ExitsZero() {
        (string _, string _, int exitCode) = RunRepl("exit");
        Assert.Equal(0, exitCode);
    }

    // -----------------------------------------------------------------------
    // REPL — expression auto-print
    // -----------------------------------------------------------------------

    [Fact]
    public void Repl_BareIntExpression_PrintsResult() {
        (string stdout, string stderr, int exitCode) = RunRepl("2 + 3\nexit");

        Assert.Equal(0, exitCode);
        Assert.Contains("5", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void Repl_BareStringLiteral_PrintsValue() {
        (string stdout, string stderr, int exitCode) = RunRepl("\"hello\"\nexit");

        Assert.Equal(0, exitCode);
        Assert.Contains("hello", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void Repl_PrintCall_DoesNotDoublePrint() {
        // print(42) should output "42" exactly once, not "42\n42".
        (string stdout, string _, int _) = RunRepl("print(42)\nexit");

        int count = 0;
        int pos = 0;
        while ((pos = stdout.IndexOf("42", pos, StringComparison.Ordinal)) >= 0) {
            count++;
            pos++;
        }
        // "42" appears in the output exactly once (from print(42) itself).
        Assert.Equal(1, count);
    }

    // -----------------------------------------------------------------------
    // REPL — declaration does not auto-print
    // -----------------------------------------------------------------------

    [Fact]
    public void Repl_Declaration_DoesNotPrintValue() {
        (string stdout, string stderr, int exitCode) = RunRepl("x := 99\nexit");

        Assert.Equal(0, exitCode);
        // Only the banner, prompt characters etc. appear — not the value 99.
        // We assert stderr is clean (no error) and the literal "99" does not
        // appear as a result print (it might appear in a prompt, so we check
        // stderr is empty as the main correctness signal).
        Assert.Equal(string.Empty, stderr);
        // The value should NOT be auto-printed as a separate output line.
        // Strip the banner/prompt/blank lines and confirm no bare "99" result line.
        string[] lines = stdout.Split(NL, StringSplitOptions.RemoveEmptyEntries);
        bool hasAutoResult = lines.Any(l => l.Trim() == "99");
        Assert.False(hasAutoResult, $"Declaration should not auto-print. Stdout:\n{stdout}");
    }

    // -----------------------------------------------------------------------
    // REPL — persistent session scope
    // -----------------------------------------------------------------------

    [Fact]
    public void Repl_LaterEntrySeesEarlierDeclaration() {
        // Entry 1 declares x; Entry 2 reads and prints x.
        const string input = "x := 77\nprint(x)\nexit";
        (string stdout, string stderr, int exitCode) = RunRepl(input);

        Assert.Equal(0, exitCode);
        Assert.Contains("77", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void Repl_AssignmentInLaterEntry_UpdatesValue() {
        const string input = "n := 10\nn = 20\nprint(n)\nexit";
        (string stdout, string stderr, int exitCode) = RunRepl(input);

        Assert.Equal(0, exitCode);
        Assert.Contains("20", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void Repl_NullableVarFromEarlierEntry_VisibleInLaterEntry() {
        const string input = "maybe: string? := nil\nresult := maybe ?? \"default\"\nprint(result)\nexit";
        (string stdout, string stderr, int exitCode) = RunRepl(input);

        Assert.Equal(0, exitCode);
        Assert.Contains("default", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // REPL — multi-line block
    // -----------------------------------------------------------------------

    [Fact]
    public void Repl_MultilineBlock_ReadsToClosingBrace() {
        // The user opens a block with '{'; the REPL should collect lines
        // until the brace closes, then compile and execute.
        const string input = "val := 5\n{\n    val = 10\n}\nprint(val)\nexit";
        (string stdout, string stderr, int exitCode) = RunRepl(input);

        Assert.Equal(0, exitCode);
        Assert.Contains("10", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // REPL — compile error reports to stderr and session continues
    // -----------------------------------------------------------------------

    [Fact]
    public void Repl_CompileError_WritesStderrAndContinues() {
        // Intentional type error: cannot add int and string.
        // After the error the REPL should still accept the next entry.
        const string input = "1 + \"oops\"\nprint(42)\nexit";
        (string stdout, string stderr, int exitCode) = RunRepl(input);

        Assert.Equal(0, exitCode);
        // Error was reported to stderr.
        Assert.False(string.IsNullOrWhiteSpace(stderr),
            "Expected a compile error on stderr.");
        // The REPL continued: the subsequent print(42) executed.
        Assert.Contains("42", stdout);
    }
}
