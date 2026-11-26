using System.Diagnostics;

using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Runtime;
using Grob.Stdlib;
using Grob.Torture.Fakes;
using Grob.Vm;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Torture;

/// <summary>
/// One run of one source through the pipeline, on the oracle's worker thread. It records the
/// phase it is in, so the watchdog can name the phase a stalled run is stuck in, and when the
/// VM started, so the oracle's waiting thread can cancel the VM at its bound. It classifies how
/// the run ended (gate report §1.1).
/// </summary>
/// <remarks>
/// The VM's token is cancelled by the waiting thread through <see cref="CancelVm"/>, not by a
/// timed <see cref="CancellationTokenSource"/>. A timed source cancels from a thread-pool timer,
/// and on a loaded two-core CI runner that callback was starved past the watchdog, so a VM that
/// honours its token within 256 steps was reported as stalled.
/// </remarks>
internal sealed class OracleRun : IDisposable {
    private static readonly string[] GrobNamespaces = ["Grob.Compiler", "Grob.Vm", "Grob.Stdlib"];

    private readonly string _source;
    private readonly string _fileName;
    private readonly HostFakes _fakes;
    private readonly Action<PipelinePhase>? _phaseHook;
    private readonly List<Finding> _findings = [];
    private readonly DiagnosticBag _bag = new();
    private readonly CancellationTokenSource _vmCancellation = new();
    private int _audited;
    private int[]? _lineLengths;
    private long _vmStarted;
    private volatile PipelinePhase _phase;

    /// <summary>Prepares a run; nothing happens until <see cref="Execute"/>.</summary>
    public OracleRun(string source, string fileName, HostFakes fakes, Action<PipelinePhase>? phaseHook) {
        _source = source;
        _fileName = fileName;
        _fakes = fakes;
        _phaseHook = phaseHook;
    }

    /// <summary>The phase the run is in, or ended in.</summary>
    public PipelinePhase Phase => _phase;

    /// <summary>How long the VM has been running; zero before it starts.</summary>
    public TimeSpan VmElapsed {
        get {
            long started = Interlocked.Read(ref _vmStarted);
            return started == 0 ? TimeSpan.Zero : Stopwatch.GetElapsedTime(started);
        }
    }

    /// <summary>Cancels the VM's token; the VM stops at its next budget check (D-319).</summary>
    public void CancelVm() => _vmCancellation.Cancel();

    /// <summary>Releases the token source. Call only once the worker has finished.</summary>
    public void Dispose() => _vmCancellation.Dispose();

    /// <summary>How the run ended; <see langword="null"/> until <see cref="Execute"/> returns.</summary>
    public OracleResult? Result { get; private set; }

    /// <summary>
    /// Runs the pipeline and classifies the result. This is the oracle's outermost frame for the
    /// run, so it catches every exception: classifying them is its whole purpose. A
    /// <see cref="StackOverflowException"/> cannot be caught and ends the process (F-3).
    /// </summary>
    public void Execute() {
        try {
            Result = RunPipeline();
        } catch (GrobExitException exit) {
            Result = ClassifyExit(exit.Code);
        } catch (GrobRuntimeException runtime) {
            Result = ClassifyRuntimeError(runtime);
        } catch (OperationCanceledException) when (_phase == PipelinePhase.Run && _vmCancellation.IsCancellationRequested) {
            Result = Conclude(OutcomeKind.Bounded);
        } catch (Exception exception) {
            _findings.Add(new Finding(
                new Signature(_phase, FindingClass.Exception, $"{exception.GetType().Name}: {MessageNormaliser.Normalise(exception.Message)}"),
                exception.Message,
                FirstGrobFrame(HostCause(exception))));
            Result = Conclude(OutcomeKind.Finding);
        }
    }

    private OracleResult RunPipeline() {
        Enter(PipelinePhase.Catalogue);
        NativeCatalogue catalogue = StdlibCatalogue.Build(
            _fakes.RandomSource, _fakes.Environment, _fakes.Streams, _fakes.Clock, _fakes.FileSystem, verbose: false);

        Enter(PipelinePhase.Lex);
        IReadOnlyList<Token> tokens = Lexer.Scan(_source, _bag, _fileName);
        AuditNewDiagnostics(PipelinePhase.Lex);

        Enter(PipelinePhase.Parse);
        CompilationUnit unit = Parser.Parse(tokens, _bag);
        AuditNewDiagnostics(PipelinePhase.Parse);

        Enter(PipelinePhase.Check);
        new TypeChecker(_bag, catalogue).Check(unit);
        AuditNewDiagnostics(PipelinePhase.Check);
        _findings.AddRange(SentinelAudit.Audit(unit, _bag.Diagnostics.Select(d => d.Code)));
        if (_bag.HasErrors) return Conclude(OutcomeKind.Diagnostics);

        Enter(PipelinePhase.Compile);
        Chunk chunk = GrobCompiler.Compile(unit, _bag);
        AuditNewDiagnostics(PipelinePhase.Compile);
        if (_bag.HasErrors) return Conclude(OutcomeKind.Diagnostics);

        Enter(PipelinePhase.Run);
        var vm = new VirtualMachine(_fakes.Streams, catalogue);
        Interlocked.Exchange(ref _vmStarted, Stopwatch.GetTimestamp());
        vm.Run(chunk, _vmCancellation.Token);
        return Conclude(OutcomeKind.Clean);
    }

    private void Enter(PipelinePhase phase) {
        _phase = phase;
        _phaseHook?.Invoke(phase);
    }

    // Each phase's new diagnostics are audited under that phase, so a location finding names
    // the phase that reported the diagnostic.
    private void AuditNewDiagnostics(PipelinePhase phase) {
        IReadOnlyList<Diagnostic> all = _bag.Diagnostics;
        for (; _audited < all.Count; _audited++) {
            _lineLengths ??= DiagnosticAudit.LineLengths(_source);
            _findings.AddRange(DiagnosticAudit.Audit(all[_audited], phase, _lineLengths, _fileName));
        }
    }

    private OracleResult ClassifyExit(int code) {
        if (code is < 0 or > 255) {
            string bucket = code < 0 ? "negative" : "above-255";
            _findings.Add(new Finding(new Signature(PipelinePhase.Run, FindingClass.ExitRange, bucket), $"exit({code})"));
        }
        return Conclude(OutcomeKind.Exit, code);
    }

    private OracleResult ClassifyRuntimeError(GrobRuntimeException runtime) {
        if (!DiagnosticAudit.IsRegistered(runtime.Code)) {
            _findings.Add(new Finding(new Signature(_phase, FindingClass.Unregistered, runtime.Code), runtime.Message));
        }
        return Conclude(OutcomeKind.RuntimeError);
    }

    private OracleResult Conclude(OutcomeKind outcome, int? exitCode = null) =>
        new(_findings.Count > 0 ? OutcomeKind.Finding : outcome, [.. _findings], exitCode);

    /// <summary>
    /// The exception whose stack names the defect. The VM reports a host exception out of a native
    /// as a <see cref="GrobInternalException"/> (D-459), which is thrown from the VM's wrapper, so
    /// its own first frame is the same for every native; the host exception it carries was thrown
    /// where the defect is. The finding's signature still comes from the reported exception.
    /// </summary>
    private static Exception HostCause(Exception exception) =>
        exception is GrobInternalException { InnerException: { } host } ? host : exception;

    private static string? FirstGrobFrame(Exception exception) {
        foreach (StackFrame frame in new StackTrace(exception, fNeedFileInfo: false).GetFrames()) {
            if (frame.GetMethod() is { DeclaringType: { } type } method && IsGrobPipeline(type.Namespace ?? "")) {
                return $"{type.FullName}.{method.Name}";
            }
        }
        return null;
    }

    private static bool IsGrobPipeline(string ns) => GrobNamespaces.Any(g => ns.StartsWith(g, StringComparison.Ordinal));
}
