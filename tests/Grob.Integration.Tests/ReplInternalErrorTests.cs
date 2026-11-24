using Grob.Cli;
using Grob.Core;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// The REPL's handling of an internal error and of <c>exit(n)</c> (D-451 Decision 2, gate report
/// §2.1 and §2.4; rulings Q3 and Q4). An internal error in one entry is reported as E9001 and the
/// session carries on, since a REPL that dies on one bad entry loses the user's session; a
/// script's <c>exit(n)</c> ends the session and returns <c>n</c> (closing R-72).
/// </summary>
public sealed class ReplInternalErrorTests {
    private static string NL => Environment.NewLine;

    private static (string Stdout, string Stderr, int ExitCode) RunRepl(string input, Action<PipelinePhase>? hook = null) {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        int exitCode = new ReplCommand(new StringReader(input), stdout, stderr, verbose: false,
            HostCapabilities.System, hook).Run();
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    // Throws once, at the first entry to the given phase, then lets every later entry run.
    private static Action<PipelinePhase> ThrowOnceAt(PipelinePhase target, Exception exception) {
        bool thrown = false;
        return phase => {
            if (phase != target || thrown) return;
            thrown = true;
            throw exception;
        };
    }

    [Theory]
    [InlineData(nameof(PipelinePhase.Lexing), "lexing")]
    [InlineData(nameof(PipelinePhase.Parsing), "parsing")]
    [InlineData(nameof(PipelinePhase.TypeChecking), "type checking")]
    [InlineData(nameof(PipelinePhase.Compiling), "compiling")]
    [InlineData(nameof(PipelinePhase.Running), "running")]
    public void HostException_InAnEntry_IsReportedAsE9001_AndTheSessionContinues(string phaseName, string words) {
        (string stdout, string stderr, int exitCode) = RunRepl(
            $"1 + 1{NL}40 + 2{NL}exit{NL}",
            ThrowOnceAt(Enum.Parse<PipelinePhase>(phaseName), new InvalidOperationException("secret detail")));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(
            $"error[E9001]: {ErrorCatalog.E9001.Title}{NL}" +
            $"  --> <repl> (while {words}){NL}" +
            $"  = InvalidOperationException{NL}" +
            $"  = this is a bug in Grob, not in your script; please report it with the script at https://github.com/grob-lang/grob/issues{NL}",
            stderr);
        Assert.DoesNotContain("secret detail", stderr);
        // The first entry failed; the second ran in the same session and printed its result.
        Assert.Contains("42", stdout);
    }

    [Fact]
    public void InternalException_InAnEntry_IsReportedWithItsMessage() {
        (_, string stderr, int exitCode) = RunRepl(
            $"1 + 1{NL}exit{NL}",
            ThrowOnceAt(PipelinePhase.Running, new GrobInternalException("GrobValue kind mismatch: expected Float, actual Int.")));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("  = GrobInternalException: GrobValue kind mismatch: expected Float, actual Int." + NL, stderr);
    }

    [Fact]
    public void GrobRuntimeError_InAnEntry_KeepsItsOwnReport_AndTheSessionContinues() {
        (string stdout, string stderr, int exitCode) = RunRepl(
            $"1 + 1{NL}40 + 2{NL}exit{NL}",
            ThrowOnceAt(PipelinePhase.Running, new GrobRuntimeException("E5002", 1, "Division by zero.")));

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal($"error[E5002]: Division by zero.{NL}  --> <repl>:1{NL}", stderr);
        Assert.Contains("42", stdout);
    }

    /// <summary>
    /// A reader that fails, as a broken console stream does. It gives up after three reads, at end
    /// of input, so a session that reads again after a failure ends rather than hangs the suite.
    /// </summary>
    private sealed class FailingReader : TextReader {
        public int Reads { get; private set; }

        public override string? ReadLine() {
            if (++Reads > 3) return null;
            throw new IOException("the handle is invalid");
        }
    }

    // A read that fails would fail again on the same reader, so the session reports it once and
    // ends; it does not report it and read again for ever.
    [Fact]
    public void InputThatCannotBeRead_IsReportedOnce_AndEndsTheSession() {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var input = new FailingReader();

        int exitCode = new ReplCommand(input, stdout, stderr, verbose: false, HostCapabilities.System).Run();

        Assert.Equal(ExitCodes.CompileError, exitCode);
        Assert.Equal(1, input.Reads);
        Assert.Equal(
            $"error[E9001]: {ErrorCatalog.E9001.Title}{NL}" +
            $"  --> <repl> (while lexing){NL}" +
            $"  = IOException{NL}" +
            $"  = this is a bug in Grob, not in your script; please report it with the script at https://github.com/grob-lang/grob/issues{NL}",
            stderr.ToString());
    }

    // Accepts the banner, then fails every flush of a prompt, giving up after three so that a
    // session that retries shows as three reports rather than a hung test.
    private sealed class PromptRejectingWriter : StringWriter {
        public int Flushes { get; private set; }

        public override void Flush() {
            if (++Flushes > 3) return;
            throw new IOException("the pipe is being closed");
        }
    }

    // A prompt that cannot be written would fail again on the same writer, so the session ends
    // as it does on a failed read.
    [Fact]
    public void PromptThatCannotBeWritten_IsReportedOnce_AndEndsTheSession() {
        using var stdout = new PromptRejectingWriter();
        using var stderr = new StringWriter();

        int exitCode = new ReplCommand(new StringReader($"1 + 1{NL}"), stdout, stderr, verbose: false, HostCapabilities.System).Run();

        Assert.Equal(ExitCodes.CompileError, exitCode);
        Assert.Equal(1, stdout.Flushes);
        Assert.Equal(
            $"error[E9001]: {ErrorCatalog.E9001.Title}{NL}" +
            $"  --> <repl> (while lexing){NL}" +
            $"  = IOException{NL}" +
            $"  = this is a bug in Grob, not in your script; please report it with the script at https://github.com/grob-lang/grob/issues{NL}",
            stderr.ToString());
    }

    [Fact]
    public void ScriptExit_EndsTheSession_AndReturnsItsCode() {
        (string stdout, string stderr, int exitCode) = RunRepl($"exit(7){NL}print(\"not reached\"){NL}");

        Assert.Equal(7, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.DoesNotContain("not reached", stdout);
    }

    [Fact]
    public void ScriptExit_ThrownInAPhase_IsNotAnInternalError() {
        (_, string stderr, int exitCode) = RunRepl(
            $"1 + 1{NL}",
            ThrowOnceAt(PipelinePhase.Compiling, new GrobExitException(9)));

        Assert.Equal(9, exitCode);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void CleanEntry_CallsEachPhaseHookInOrder() {
        var seen = new List<PipelinePhase>();

        (_, _, int exitCode) = RunRepl($"1 + 1{NL}exit{NL}", seen.Add);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(
            [PipelinePhase.Lexing, PipelinePhase.Parsing, PipelinePhase.TypeChecking, PipelinePhase.Compiling, PipelinePhase.Running],
            seen);
    }
}
