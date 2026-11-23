using Grob.Torture.Fakes;

namespace Grob.Torture;

/// <summary>
/// The crash oracle (D-451): runs one source text through the lexer, parser, checker, compiler
/// and VM in process, against fake capabilities, and classifies how the run ended. No program
/// text, valid or not, may make Grob raise an internal error or throw a host exception.
/// </summary>
/// <remarks>
/// Every run is on its own worker thread, so a phase the VM's cancellation token does not reach
/// is still bounded: the oracle waits for the thread up to the budget's watchdog, and a run past
/// it is a timeout finding. A .NET thread cannot be killed, so a stalled worker is abandoned as a
/// background thread; a caller that sees a timeout should stop, not run on beside it. The worker
/// has a 1 MB stack, the CLI's main-thread stack on Windows, so a deep program overflows here
/// where it would overflow for a user (F-3), and is not hidden by a larger stack.
/// </remarks>
/// <param name="budget">The run's bounds.</param>
/// <param name="fakes">Builds each run's capabilities; <see cref="HostFakes.Create"/> when omitted.</param>
/// <param name="phaseHook">
/// Called on the worker as each phase starts. A test seam: a hook that throws makes that phase
/// fail on demand, and one that blocks makes it stall, without depending on an open defect.
/// </param>
internal sealed class Oracle(OracleBudget budget, Func<HostFakes>? fakes = null, Action<PipelinePhase>? phaseHook = null) {
    /// <summary>The file name a run gives the lexer when the caller names none.</summary>
    public const string DefaultFileName = "program.grob";

    private const int WorkerStackSize = 1024 * 1024;

    private readonly Func<HostFakes> _fakes = fakes ?? HostFakes.Create;

    /// <summary>Runs <paramref name="source"/> and classifies the result.</summary>
    /// <param name="source">The program text.</param>
    /// <param name="fileName">The file name diagnostics must carry.</param>
    public OracleResult Run(string source, string fileName = DefaultFileName) {
        ArgumentNullException.ThrowIfNull(source);
        var run = new OracleRun(source, fileName, budget, _fakes(), phaseHook);
        var worker = new Thread(run.Execute, WorkerStackSize) { IsBackground = true, Name = "grob-torture-oracle" };
        worker.Start();
        if (worker.Join(budget.Watchdog)) {
            return run.Result!;
        }
        return new OracleResult(OutcomeKind.Finding, [
            new Finding(new Signature(run.Phase, FindingClass.Timeout, "watchdog"),
                $"the run did not end within {budget.Watchdog.TotalMilliseconds} ms"),
        ]);
    }
}
