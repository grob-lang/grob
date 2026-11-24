using Grob.Cli;
using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// The CLI reports an internal error or a host exception from any phase as E9001, with the
/// bug-report line and no host stack trace (D-451 Decision 2; gate report §2.3). A phase hook
/// makes each phase throw on demand, so no test depends on an open defect's reproducer. The
/// exit code is <c>2</c> when the script never ran and <c>1</c> when it was running
/// (<see cref="ExitCodes"/>).
/// </summary>
public sealed class InternalErrorReportTests {
    private const string BugReportLine =
        "  = this is a bug in Grob, not in your script; please report it with the script at https://github.com/grob-lang/grob/issues";

    private static string NL => Environment.NewLine;

    public static TheoryData<string, string, int> Phases => new() {
        { nameof(PipelinePhase.LoadingStandardLibrary), "loading the standard library", ExitCodes.CompileError },
        { nameof(PipelinePhase.Lexing), "lexing", ExitCodes.CompileError },
        { nameof(PipelinePhase.Parsing), "parsing", ExitCodes.CompileError },
        { nameof(PipelinePhase.TypeChecking), "type checking", ExitCodes.CompileError },
        { nameof(PipelinePhase.Compiling), "compiling", ExitCodes.CompileError },
        { nameof(PipelinePhase.Running), "running", ExitCodes.RuntimeError },
    };

    private static (string Stdout, string Stderr, int ExitCode, string Path) RunWithHook(
            Action<PipelinePhase> hook, string source = "print(\"ok\")\n", bool verbose = false,
            Func<HostCapabilities>? capabilities = null) {
        string path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            using var stdout = new StringWriter();
            using var stderr = new StringWriter();
            var command = new RunCommand(stdout, stderr, TextReader.Null, verbose,
                capabilities ?? HostCapabilities.System, hook);
            int exitCode = command.Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode, path);
        } finally {
            File.Delete(path);
        }
    }

    private static Action<PipelinePhase> ThrowAt(PipelinePhase target, Exception exception) =>
        phase => {
            if (phase == target) throw exception;
        };

    [Theory]
    [MemberData(nameof(Phases))]
    public void HostException_InAnyPhase_IsReportedAsE9001(string phaseName, string words, int expectedExit) {
        (string stdout, string stderr, int exitCode, string path) =
            RunWithHook(ThrowAt(Enum.Parse<PipelinePhase>(phaseName), new InvalidOperationException("secret detail")));

        Assert.Equal(expectedExit, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal(
            $"error[E9001]: {ErrorCatalog.E9001.Title}{NL}" +
            $"  --> {path} (while {words}){NL}" +
            $"  = InvalidOperationException{NL}" +
            BugReportLine + NL,
            stderr);
    }

    [Theory]
    [MemberData(nameof(Phases))]
    public void InternalException_InAnyPhase_IsReportedWithItsMessage(string phaseName, string words, int expectedExit) {
        (_, string stderr, int exitCode, string path) =
            RunWithHook(ThrowAt(Enum.Parse<PipelinePhase>(phaseName), new GrobInternalException("GrobValue kind mismatch: expected Float, actual Int.")));

        Assert.Equal(expectedExit, exitCode);
        Assert.Equal(
            $"error[E9001]: {ErrorCatalog.E9001.Title}{NL}" +
            $"  --> {path} (while {words}){NL}" +
            $"  = GrobInternalException: GrobValue kind mismatch: expected Float, actual Int.{NL}" +
            BugReportLine + NL,
            stderr);
    }

    [Fact]
    public void HostException_WithoutVerbose_PrintsNeitherItsMessageNorAStackTrace() {
        (_, string stderr, _, _) = RunWithHook(
            ThrowAt(PipelinePhase.Compiling, new InvalidOperationException("secret detail")));

        Assert.DoesNotContain("secret detail", stderr);
        Assert.DoesNotContain("   at ", stderr);
    }

    [Fact]
    public void HostException_WithVerbose_AddsTheMessageAndTheStackTrace() {
        (_, string stderr, int exitCode, _) = RunWithHook(
            phase => ThrowFromAMethod(phase, PipelinePhase.Compiling), verbose: true);

        Assert.Equal(ExitCodes.CompileError, exitCode);
        Assert.Contains("error[E9001]:", stderr);
        Assert.Contains("InvalidOperationException: secret detail", stderr);
        Assert.Contains("   at ", stderr);
        Assert.True(
            stderr.IndexOf(BugReportLine, StringComparison.Ordinal) < stderr.IndexOf("   at ", StringComparison.Ordinal),
            "the verbose detail follows the bug-report line");
    }

    // A real stack frame, so the verbose report has a trace to print.
    private static void ThrowFromAMethod(PipelinePhase phase, PipelinePhase target) {
        if (phase == target) throw new InvalidOperationException("secret detail");
    }

    [Fact]
    public void HostException_FromANativeDuringTheRun_IsReportedAsE9001() {
        // No hook: date.now() reads the clock inside a native, during dispatch.
        var fakes = new HostCapabilities(new FixedRandomSource(), new MapEnvironment(), new ThrowingClock(),
            new FixedFileSystem(FixedFileSystem.HarnessDirectory));

        (_, string stderr, int exitCode, string path) =
            RunWithHook(_ => { }, "print(date.now())\n", capabilities: () => fakes);

        Assert.Equal(ExitCodes.RuntimeError, exitCode);
        Assert.Equal(
            $"error[E9001]: {ErrorCatalog.E9001.Title}{NL}" +
            $"  --> {path} (while running){NL}" +
            $"  = InvalidOperationException{NL}" +
            BugReportLine + NL,
            stderr);
    }

    [Fact]
    public void GrobRuntimeError_InTheRun_KeepsItsOwnReport() {
        (string stdout, string stderr, int exitCode, string path) = RunWithHook(
            ThrowAt(PipelinePhase.Running, new GrobRuntimeException("E5002", 3, "Division by zero.")));

        Assert.Equal(ExitCodes.RuntimeError, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal($"error[E5002]: Division by zero.{NL}  --> {path}:3{NL}", stderr);
        Assert.DoesNotContain("E9001", stderr);
    }

    [Fact]
    public void ScriptExit_InTheRun_IsNotAnInternalError() {
        (string stdout, string stderr, int exitCode, _) = RunWithHook(
            ThrowAt(PipelinePhase.Running, new GrobExitException(42)));

        Assert.Equal(42, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void CleanRun_CallsEachPhaseHookInOrder() {
        var seen = new List<PipelinePhase>();

        (_, _, int exitCode, _) = RunWithHook(seen.Add);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(
            [PipelinePhase.LoadingStandardLibrary, PipelinePhase.Lexing, PipelinePhase.Parsing,
                PipelinePhase.TypeChecking, PipelinePhase.Compiling, PipelinePhase.Running],
            seen);
    }

    [Fact]
    public void CompileDiagnostics_AreNotAnInternalError() {
        (_, string stderr, int exitCode, _) = RunWithHook(_ => { }, "x: int := \"text\"\n");

        Assert.Equal(ExitCodes.CompileError, exitCode);
        Assert.DoesNotContain("E9001", stderr);
    }

    private sealed class ThrowingClock : IClock {
        public DateTime UtcNow => throw new InvalidOperationException("clock failed");

        public TimeZoneInfo LocalZone => TimeZoneInfo.Utc;
    }
}
