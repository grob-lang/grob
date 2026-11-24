using Grob.Torture.Findings;

namespace Grob.Torture.Campaign;

/// <summary>
/// One campaign worker (gate report §4.4): runs the programs of its shard of the plan that have no
/// result yet, so a restarted worker resumes where its last one stopped. For each program it
/// keeps its journal on the program and phase, appends the result, and minimises each finding
/// there and then, so minimising runs in parallel across the workers, and stores its record.
/// </summary>
/// <remarks>
/// <para>
/// A run past its watchdog leaves a thread that cannot be stopped, so after one, whether the
/// program's own run or a candidate while minimising, the worker finishes the program and returns
/// <see cref="RestartExitCode"/>: its supervisor starts a fresh process, and the stalled thread
/// ends with the old one. A program that timed out is stored with itself as its reproducer, since
/// minimising it would stall again at every candidate.
/// </para>
/// <para>
/// Candidates are run at <paramref name="minimiseBudget"/>, in practice <see cref="OracleBudget.Minimise"/>,
/// not at the program's: a candidate that loops is common once lines are deleted, and each would
/// cost the program's bound.
/// </para>
/// </remarks>
/// <param name="directory">The campaign's directory.</param>
/// <param name="shard">The worker's shard.</param>
/// <param name="write">Writes a planned program's source.</param>
/// <param name="programBudget">The bound on each program's run.</param>
/// <param name="minimiseBudget">The bound on each candidate's run while minimising.</param>
/// <param name="hooks">The test seams; none when omitted.</param>
internal sealed class CampaignWorker(
        CampaignDirectory directory,
        int shard,
        Func<PlannedProgram, string> write,
        OracleBudget programBudget,
        OracleBudget minimiseBudget,
        WorkerHooks? hooks = null) {
    /// <summary>The exit code a worker returns when it must be restarted: <c>EX_TEMPFAIL</c>.</summary>
    public const int RestartExitCode = 75;

    private readonly WorkerHooks _hooks = hooks ?? new WorkerHooks();
    private Journal? _journal;
    private string _current = "";

    /// <summary>Runs the shard and returns the process exit code: 0 when it is done.</summary>
    public int Run() {
        CampaignSettings settings = directory.ReadSettings()
            ?? throw new InvalidOperationException($"{directory.Root} holds no campaign");
        var done = new HashSet<string>(directory.ReadResults(shard).Select(r => r.ProgramId), StringComparer.Ordinal);
        var store = new FindingStore(directory.FindingsPath);
        var oracle = new Oracle(programBudget, phaseHook: phase => {
            Record(_current, phase.ToString());
            _hooks.Phase?.Invoke(_current, phase);
        });

        using (_journal = new Journal(directory.JournalPath(shard))) {
            foreach (PlannedProgram program in directory.ReadPlan()) {
                if (CampaignPlan.ShardOf(program.Id, settings.Workers) != shard || done.Contains(program.Id)) continue;
                bool restart = RunOne(program, oracle, store);
                Clear();
                if (restart) return RestartExitCode;
            }
        }
        return 0;
    }

    // Runs one program; returns whether a run stalled, after which the worker must be restarted.
    private bool RunOne(PlannedProgram program, Oracle oracle, FindingStore store) {
        _current = program.Id;
        string source = write(program);
        OracleResult result = oracle.Run(source);
        directory.AppendResult(shard, new ProgramResult(program.Id, result.Outcome, result.Findings));
        if (result.Findings.Count == 0) return false;

        if (result.TimedOut) {
            StoreUnminimised(directory, shard, store, program.Id, source, result.Findings, "the run passed its watchdog, so it was not minimised");
            return true;
        }

        Record(program.Id, Journal.Minimising);
        return Minimise(store, program.Id, source, result.Findings);
    }

    private bool Minimise(FindingStore store, string programId, string source, IReadOnlyList<Finding> findings) {
        bool stalled = false;
        var candidates = new Oracle(minimiseBudget, phaseHook: _hooks.Minimise);
        IReadOnlyList<Finding> Run(string candidate) {
            if (stalled) return [];
            OracleResult result = candidates.Run(candidate);
            stalled = result.TimedOut;
            return stalled ? [] : result.Findings;
        }

        new Triager(Run, store).Triage(programId, source, findings);
        if (stalled) directory.AppendUnminimised(shard, programId, "a minimising candidate passed its watchdog");
        return stalled;
    }

    /// <summary>
    /// Stores a record for each finding with the program as its own reproducer, and records why it
    /// was not minimised: the worker's path after a stall, and the supervisor's after a death or for
    /// a result that has no record. A record that already exists is kept as it is.
    /// </summary>
    public static void StoreUnminimised(CampaignDirectory directory, int shard, FindingStore store, string programId, string source,
            IReadOnlyList<Finding> findings, string reason) {
        foreach (Finding finding in findings) {
            store.Add(new FindingRecord(programId, finding.Signature.Key, finding.Message, finding.FirstFrame, source, source, false));
        }
        directory.AppendUnminimised(shard, programId, reason);
    }

    private void Record(string programId, string state) {
        _journal!.Write(programId, state);
        _hooks.Journal?.Invoke($"{programId}\t{state}");
    }

    private void Clear() {
        _journal!.Clear();
        _hooks.Journal?.Invoke("");
    }
}
