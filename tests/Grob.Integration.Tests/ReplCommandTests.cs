using System.Text;
using Grob.Cli;
using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Integration tests for <see cref="ReplCommand"/>: drives the REPL loop through
/// <see cref="ReplCommand.Run"/> with injected <see cref="System.IO.StringReader"/>
/// input and captured <see cref="System.IO.StringWriter"/> streams.
/// </summary>
public sealed class ReplCommandTests {
    private static string NL => Environment.NewLine;

    private static (string Stdout, string Stderr, int ExitCode) RunRepl(string input) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new ReplCommand(new StringReader(input), stdout, stderr).Run();
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    // -----------------------------------------------------------------------
    // EOF and exit command
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_EmptyInput_ReturnsZeroAndPrintsBanner() {
        (string stdout, _, int exitCode) = RunRepl(string.Empty);

        Assert.Equal(0, exitCode);
        Assert.Matches(@"Grob \d+\.\d+\.\d+", stdout);
    }

    [Fact]
    public void Run_ExitCommand_ReturnsZero() {
        (_, _, int exitCode) = RunRepl("exit");
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void Run_BlankLine_IgnoredAndSessionContinues() {
        // An empty line is trimmed to zero length — the REPL skips it rather than
        // treating it as source to compile.
        (_, string stderr, int exitCode) = RunRepl($"{NL}exit");
        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
    }

    // -----------------------------------------------------------------------
    // Auto-print — bare expressions
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_BareIntExpression_AutoPrintsResult() {
        (string stdout, string stderr, int exitCode) = RunRepl($"2 + 2{NL}exit");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("4", stdout);
    }

    [Fact]
    public void Run_BareStringExpression_AutoPrintsResult() {
        (string stdout, _, _) = RunRepl($"\"hello\"{NL}exit");
        Assert.Contains("hello", stdout);
    }

    [Fact]
    public void Run_PrintCallIsNotDoubleprinted() {
        // print(x) is a side-effecting built-in — it must NOT be auto-printed again.
        (string stdout, _, _) = RunRepl($"print(42){NL}exit");

        // Anchored on the prompt prefix, not a bare "42" substring search: the
        // REPL banner embeds MinVer's build height (e.g. "0.5.0-alpha.0.142"),
        // which can itself contain "42" and produce a false second match.
        int count = 0;
        int idx = -1;
        while ((idx = stdout.IndexOf("G> 42", idx + 1, StringComparison.Ordinal)) >= 0)
            count++;
        Assert.Equal(1, count);
    }

    // -----------------------------------------------------------------------
    // Session state — variable persistence across entries
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_VarDeclThenBareRef_AutoPrintsPersistedValue() {
        // 'x := 42' stores x in VM globals; the next entry 'x' synthesises a
        // preamble 'x := 42' before compiling, then auto-prints.
        (string stdout, string stderr, int exitCode) = RunRepl($"x := 42{NL}x{NL}exit");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("42", stdout);
    }

    [Fact]
    public void Run_FloatVarPersists_AutoPrintIncludesDecimal() {
        // FormatFloat must ensure a decimal point so the preamble literal parses
        // as float, not int, in the next entry.
        (string stdout, string stderr, _) = RunRepl($"x := 1.5{NL}x{NL}exit");

        Assert.Equal(string.Empty, stderr);
        Assert.Contains("1.5", stdout);
    }

    [Fact]
    public void Run_StringVarWithQuotes_RoundtripsInPreamble() {
        // EscapeStringLiteral must escape double-quotes so the synthesised preamble
        // is valid Grob source on the next entry.
        (string stdout, string stderr, _) = RunRepl($"s := \"it's \\\"ok\\\"\"{NL}s{NL}exit");

        Assert.Equal(string.Empty, stderr);
        Assert.Contains("it's \"ok\"", stdout);
    }

    [Fact]
    public void Run_BoolVarPersists_AutoPrintsCorrectly() {
        (string stdout, string stderr, _) = RunRepl($"b := true{NL}b{NL}exit");
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("true", stdout);
    }

    [Fact]
    public void Run_NullableVarWithNilValue_SynthesisesPreambleWithTypeAnnotation() {
        // A nil-valued global needs a type annotation in the preamble so the
        // type-checker accepts it.  Declaring 'n: int? := nil' stores NullableInt;
        // the preamble must emit 'n: int? := nil', not 'n := nil'.
        (string stdout, string stderr, _) = RunRepl($"n: int? := nil{NL}n{NL}exit");

        Assert.Equal(string.Empty, stderr);
        Assert.Contains("nil", stdout);
    }

    // -----------------------------------------------------------------------
    // Multi-line input (continuation prompt)
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_MultiLineBlock_ReadsUntilBracketsBalance() {
        // Opening '{' triggers continuation; the block must compile and run cleanly.
        string input = $"{{{NL}x := 1{NL}}}{NL}exit";
        (string stdout, string stderr, int exitCode) = RunRepl(input);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("..>", stdout);
    }

    [Fact]
    public void Run_EofMidBlock_ExitsCleanly() {
        // A StringReader that ends while the bracket count is > 0 must not crash.
        // The REPL treats EOF mid-block as an incomplete entry and breaks the loop.
        (_, _, int exitCode) = RunRepl("{");
        Assert.Equal(0, exitCode);
    }

    // -----------------------------------------------------------------------
    // Error recovery — type errors and runtime errors
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_TypeErrorInEntry_WritesStderrAndContinues() {
        // A type error in one entry must be reported and the session must continue.
        string input = $"x := \"hello\" + 1{NL}42{NL}exit";
        (string stdout, string stderr, int exitCode) = RunRepl(input);

        Assert.Equal(0, exitCode);
        Assert.Contains("E0002", stderr);
        // The next entry (42) still runs and auto-prints.
        Assert.Contains("42", stdout);
    }

    [Fact]
    public void Run_RuntimeErrorInEntry_WritesStderrAndContinues() {
        // Division by zero must be reported and the session must continue.
        string input = $"a := 10{NL}b := 0{NL}a / b{NL}42{NL}exit";
        (string stdout, string stderr, int exitCode) = RunRepl(input);

        Assert.Equal(0, exitCode);
        Assert.Contains("E5002", stderr);
        // The session continues after the error.
        Assert.Contains("42", stdout);
    }

    // -----------------------------------------------------------------------
    // Help command
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_HelpCommand_PrintsHelpText() {
        (string stdout, _, _) = RunRepl($"help{NL}exit");
        Assert.Contains("exit", stdout);
        Assert.Contains("help", stdout);
    }

    // -----------------------------------------------------------------------
    // Readonly binding persists
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_ReadonlyVarPersists_AutoPrintsOnReuse() {
        (string stdout, string stderr, _) = RunRepl($"readonly pi := 3{NL}pi{NL}exit");
        Assert.Equal(string.Empty, stderr);
        Assert.Contains("3", stdout);
    }

    // -----------------------------------------------------------------------
    // ParseVersion — version string helper
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("0.5.0-alpha.0.124+abc1234", "0.5.0-alpha.0.124")]  // strips +hash
    [InlineData("1.0.0", "1.0.0")]                                    // no + → unchanged
    [InlineData(null, "unknown")]                                      // null attr → fallback
    public void ParseVersion_HandlesAllForms(string? informational, string expected) =>
        Assert.Equal(expected, ReplCommand.ParseVersion(informational));
}
