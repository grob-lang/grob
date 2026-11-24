using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

using Grob.Torture.Findings;

namespace Grob.Torture.Campaign;

/// <summary>
/// The long mode's supervisor (gate report §1.5, §4.4): builds a campaign's plan, or resumes the one
/// its directory holds, keeps one worker running on each shard until the shard is done, and then
/// writes the report, with the black-box check of every distinct reproducer through the real CLI
/// when one is given (§4.3).
/// </summary>
/// <remarks>
/// <para>
/// A worker that dies names nothing itself, since a stack overflow (R-70) or an out-of-memory runs
/// no handler, so the supervisor reads its journal. A death in a phase becomes a
/// <see cref="FindingClass.ProcessDeath"/> finding for the program the journal names, recorded as
/// that program's result so the next worker passes over it. A death while minimising leaves the
/// program's result, written before minimising began, and its record is stored unminimised. Either
/// way the shard's worker is started again.
/// </para>
/// <para>
/// A worker that dies with an empty journal, between programs or before its first, is started
/// again too, but only <see cref="MaxSilentDeaths"/> times in a row without progress, so a worker
/// that cannot start does not loop for ever.
/// </para>
/// </remarks>
/// <param name="launcher">Starts each worker and waits for it.</param>
/// <param name="corpus">Loads the seed corpus: to build a plan, and to write the source of a program a worker died on.</param>
/// <param name="grob">Runs the real CLI for the black-box check; none when omitted.</param>
/// <param name="known">The CI slice's known-findings list, which the report cites beside each signature.</param>
/// <param name="stdout">Where the summary goes.</param>
/// <param name="stderr">Where a usage error or an abandoned shard is reported.</param>
internal sealed class CampaignSupervisor(
        IWorkerLauncher launcher,
        Func<IReadOnlyList<Seed>> corpus,
        IGrobRunner? grob,
        KnownFindings? known,
        TextWriter stdout,
        TextWriter stderr) {
    /// <summary>How many times in a row a worker may die without naming a program or making progress.</summary>
    public const int MaxSilentDeaths = 3;

    private readonly Lock _stderrGate = new();

    private readonly Lazy<Dictionary<string, Seed>> _corpusById =
        new(() => corpus().ToDictionary(s => s.Id, StringComparer.Ordinal), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Runs or resumes the campaign in <paramref name="path"/> and returns the exit code: 0 when nothing was found.</summary>
    public int Run(string path, CampaignSettings settings) {
        var directory = new CampaignDirectory(path);
        CampaignSettings? existing = directory.ReadSettings();
        if (existing is not null && existing != settings) {
            stderr.WriteLine($"error: {path} holds another campaign: seed {existing.Seed}, {existing.Programs} programs, {existing.Workers} workers");
            return 3;
        }
        long started = Stopwatch.GetTimestamp();
        if (existing is null) {
            // The settings are written last: they mark the plan as whole.
            directory.WritePlan(CampaignPlan.Build(settings.Seed, settings.Programs, corpus()));
            directory.WriteSettings(settings);
        }
        Elapsed("plan", ref started);

        bool[] finished = new bool[settings.Workers];
        Parallel.For(0, settings.Workers, new ParallelOptions { MaxDegreeOfParallelism = settings.Workers },
            shard => finished[shard] = RunShard(directory, settings, shard));
        Elapsed("workers", ref started);

        bool found = Report(directory, settings);
        Elapsed("report", ref started);
        return found || finished.Contains(false) ? 1 : 0;
    }

    private void Elapsed(string step, ref long started) {
        stdout.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{step}: {Stopwatch.GetElapsedTime(started).TotalSeconds:F1} s"));
        started = Stopwatch.GetTimestamp();
    }

    // Keeps the shard's worker running until it is done; false when it was abandoned.
    private bool RunShard(CampaignDirectory directory, CampaignSettings settings, int shard) {
        int silent = 0;
        while (true) {
            int before = directory.ReadResults(shard).Count;
            int exit = launcher.Run(directory.Root, shard);
            if (exit == 0) return true;

            // The entry is consumed once read, so a later death cannot be named by this one's.
            (string ProgramId, string State)? entry = Journal.Take(directory.JournalPath(shard));
            if (exit == CampaignWorker.RestartExitCode || directory.ReadResults(shard).Count > before) silent = 0;
            if (exit == CampaignWorker.RestartExitCode) continue;

            if (entry is { } named) {
                RecordDeath(directory, settings, shard, named.ProgramId, named.State, exit);
                silent = 0;
            } else if (++silent >= MaxSilentDeaths) {
                lock (_stderrGate) stderr.WriteLine($"shard {shard}: the worker died {silent} times without naming a program; abandoned");
                return false;
            }
        }
    }

    // A program with a result already ran to its end, so the worker died after it: while
    // minimising, or before it could journal that or store every record. Its findings are stored
    // unminimised. A program with no result died running, and the death is its finding.
    private void RecordDeath(CampaignDirectory directory, CampaignSettings settings, int shard, string programId, string state, int exit) {
        string code = $"0x{exit:X8}";
        var store = new FindingStore(directory.FindingsPath);
        if (directory.ReadResults(shard).LastOrDefault(r => r.ProgramId == programId) is { } result) {
            string when = state == Journal.Minimising ? "while minimising it" : "after running it";
            CampaignWorker.StoreUnminimised(directory, shard, store, programId, Source(directory, settings, programId), result.Findings,
                $"the worker died {when}, exit {code}");
            return;
        }

        var death = new Finding(new Signature(Enum.Parse<PipelinePhase>(state), FindingClass.ProcessDeath, code), $"the worker died with exit code {code}");
        directory.AppendResult(shard, new ProgramResult(programId, OutcomeKind.Finding, [death]));
        CampaignWorker.StoreUnminimised(directory, shard, store, programId, Source(directory, settings, programId), [death],
            $"the worker died running it, exit {code}");
    }

    private string Source(CampaignDirectory directory, CampaignSettings settings, string programId) =>
        CampaignPlan.Write(directory.ReadPlan().First(p => p.Id == programId), settings.Seed, _corpusById.Value);

    // Writes the report and the summary; returns whether anything was found.
    private bool Report(CampaignDirectory directory, CampaignSettings settings) {
        IReadOnlyList<ProgramResult> results = [.. Enumerable.Range(0, settings.Workers).SelectMany(directory.ReadResults)];
        var store = new FindingStore(directory.FindingsPath);
        StoreMissingRecords(directory, settings, store, results);
        IReadOnlyList<FindingRecord> records = store.All();
        IReadOnlyList<BlackBoxResult> blackBox = grob is null ? [] : CheckReproducers(directory, records, grob, settings.Workers);

        File.WriteAllText(directory.ReportPath, CampaignReport.Build(settings, results, records, directory.ReadUnminimised(), known, blackBox));

        int signatures = results.SelectMany(r => r.Findings).Select(f => f.Signature.Key).Concat(records.Select(r => r.Signature))
            .Distinct(StringComparer.Ordinal).Count();
        stdout.WriteLine($"{results.Count} programs: " + string.Join(", ",
            results.GroupBy(r => r.Outcome).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")));
        stdout.WriteLine($"{signatures} signatures, {records.Count} records; report at {directory.ReportPath}");
        return records.Count > 0 || results.Any(r => r.Findings.Count > 0);
    }

    // A process stopped between writing a result and storing its records leaves findings with no
    // record, and the result keeps a resumed worker from running the program again; so every such
    // finding is stored unminimised before the report is written.
    private void StoreMissingRecords(CampaignDirectory directory, CampaignSettings settings, FindingStore store, IReadOnlyList<ProgramResult> results) {
        foreach (ProgramResult result in results.Where(r => r.Findings.Any(f => !File.Exists(store.PathFor(f.Signature.Key, r.ProgramId))))) {
            CampaignWorker.StoreUnminimised(directory, CampaignPlan.ShardOf(result.ProgramId, settings.Workers), store, result.ProgramId,
                Source(directory, settings, result.ProgramId), result.Findings, "its record was missing when the report was written");
        }
    }

    // Each distinct reproducer is run once, however many signatures and programs share it.
    private static List<BlackBoxResult> CheckReproducers(CampaignDirectory directory, IReadOnlyList<FindingRecord> records, IGrobRunner grob, int workers) {
        string scripts = Path.Join(directory.Root, "blackbox");
        Directory.CreateDirectory(scripts);
        var outputs = new ConcurrentDictionary<string, GrobOutput>(StringComparer.Ordinal);
        Parallel.ForEach(records.Select(r => r.Reproducer).Distinct(StringComparer.Ordinal),
            new ParallelOptions { MaxDegreeOfParallelism = workers },
            reproducer => {
                string script = Path.Join(scripts, SeedCorpus.Fnv1a64(reproducer).ToString("x16", CultureInfo.InvariantCulture) + ".grob");
                File.WriteAllText(script, reproducer);
                outputs[reproducer] = grob.Run(script);
            });

        return [.. records.Select(r => (r.Signature, r.Reproducer)).Distinct()
            .Select(pair => new BlackBoxResult(pair.Signature, pair.Reproducer, BlackBox.Classify(outputs[pair.Reproducer]), outputs[pair.Reproducer].ExitCode))];
    }
}
