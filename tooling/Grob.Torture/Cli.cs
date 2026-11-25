using Grob.Integration.Tests.ErrorExamples;
using Grob.Torture.Campaign;

namespace Grob.Torture;

/// <summary>
/// The command line. Exit codes follow Grob's documented set: <c>0</c> when nothing needs
/// triage, <c>1</c> when something does, <c>3</c> for a usage error, including a file or
/// repository that cannot be read.
/// <list type="bullet">
///   <item><description><c>check &lt;file&gt;...</c> runs each file through the oracle at the CI budget and prints its outcome and findings.</description></item>
///   <item><description><c>corpus [&lt;root&gt;]</c> runs the CI slice by hand, the seeds and their sample of mutants, and lists every finding as known or new, every stale entry and every problem with the known-findings list. The root defaults to the repository holding the working directory.</description></item>
///   <item><description><c>campaign --out &lt;dir&gt; --programs &lt;n&gt; --seed &lt;n&gt; [--workers &lt;n&gt;] [--grob &lt;path&gt;] [--root &lt;dir&gt;]</c> runs the long mode by hand (D-451 Decision 4): a plan of generated programs and mutants across worker processes, each finding minimised and recorded, and a report. Run again over the same directory, it resumes.</description></item>
///   <item><description><c>campaign-worker --out &lt;dir&gt; --shard &lt;k&gt; [--root &lt;dir&gt;]</c> is one of those workers, started by <c>campaign</c>.</description></item>
/// </list>
/// </summary>
internal static class Cli {
    private const string Usage = "usage: grob-torture check <file>... | corpus [<root>]"
        + " | campaign --out <dir> --programs <n> --seed <n> [--workers <n>] [--grob <path>] [--root <dir>]"
        + " | campaign-worker --out <dir> --shard <k> [--root <dir>]";

    private static readonly CampaignServices ProcessServices = new(root => new ProcessWorkerLauncher(root), path => new ProcessGrobRunner(path));

    // A run past its watchdog leaves a worker that cannot be stopped, so nothing more is run.
    private const string StoppedAfterTimeout = "stopped: the last run did not end, and its worker cannot be stopped";

    /// <summary>Runs one command and returns the process exit code.</summary>
    /// <param name="args">The command and its arguments.</param>
    /// <param name="stdout">Where the report goes.</param>
    /// <param name="stderr">Where usage and read errors go.</param>
    /// <param name="workingDirectory">The directory <c>corpus</c> searches upward from when no root is given.</param>
    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr, string workingDirectory) =>
        Run(args, stdout, stderr, workingDirectory, new Oracle(OracleBudget.Ci));

    /// <summary>
    /// As the public overload, over a given oracle and a given choice of programs: the seams the
    /// tests use to make a run stall and to run the seeds without their mutants.
    /// </summary>
    /// <param name="args">The command and its arguments.</param>
    /// <param name="stdout">Where the report goes.</param>
    /// <param name="stderr">Where usage and read errors go.</param>
    /// <param name="workingDirectory">The directory <c>corpus</c> searches upward from when no root is given.</param>
    /// <param name="oracle">Runs each program.</param>
    /// <param name="programs">The programs <c>corpus</c> runs from the seeds; <see cref="CorpusSlice.Programs"/> when omitted.</param>
    /// <param name="services">The processes <c>campaign</c> starts; child processes and the real CLI when omitted.</param>
    internal static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr, string workingDirectory, Oracle oracle,
            Func<IReadOnlyList<Seed>, IReadOnlyList<Seed>>? programs = null, CampaignServices? services = null) {
        programs ??= CorpusSlice.Programs;
        try {
            return args switch {
                ["check", _, ..] => Check(args.Skip(1), stdout, oracle),
                ["corpus"] => Corpus(SeedCorpus.LocateRepositoryRoot(workingDirectory), stdout, oracle, programs),
                ["corpus", string root] => Corpus(root, stdout, oracle, programs),
                ["campaign", ..] when CampaignOptions.TryParse(args, out CampaignOptions? options) =>
                    Campaign(options, stdout, stderr, workingDirectory, services ?? ProcessServices),
                ["campaign-worker", ..] when WorkerOptions.TryParse(args, out WorkerOptions? options) =>
                    Worker(options, stderr, workingDirectory),
                _ => UsageError(stderr),
            };
        } catch (Exception ex) when (CommandError(ex) is { } error) {
            stderr.WriteLine($"error: {error.Message}");
            return 3;
        }
    }

    // A file that cannot be read or written, or a CLI that cannot be started: errors in the
    // command's arguments or the machine, not in the harness. A campaign's results are stored
    // as they are made, so after one the same command resumes. Parallel work wraps the error,
    // so a wrapper holding only such errors is reported as its first.
    private static Exception? CommandError(Exception ex) => ex switch {
        IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception => ex,
        AggregateException aggregate when aggregate.InnerExceptions.All(inner => CommandError(inner) is not null) =>
            CommandError(aggregate.InnerExceptions[0]),
        _ => null,
    };

    private static int UsageError(TextWriter stderr) {
        stderr.WriteLine(Usage);
        return 3;
    }

    private static int Check(IEnumerable<string> paths, TextWriter stdout, Oracle oracle) {
        bool found = false;
        foreach (string path in paths) {
            string source = ReadFile(path);
            OracleResult result = oracle.Run(source, path);
            stdout.WriteLine($"{path}: {result.Outcome}");
            foreach (Finding finding in result.Findings) {
                stdout.WriteLine($"  {finding.Signature.Key}  {finding.Message}");
                WriteFrame(finding, stdout);
            }
            if (result.TimedOut) {
                stdout.WriteLine(StoppedAfterTimeout);
                return 1;
            }
            found |= result.Outcome == OutcomeKind.Finding;
        }
        return found ? 1 : 0;
    }

    private static int Corpus(string root, TextWriter stdout, Oracle oracle, Func<IReadOnlyList<Seed>, IReadOnlyList<Seed>> programs) {
        IReadOnlyList<Seed> seeds = programs(SeedCorpus.Load(root));
        KnownFindings known = KnownFindings.Parse(ReadFile(Path.Join(root, CorpusSlice.KnownFindingsPath)));
        DeferredWorkRegister register = DeferredWorkRegister.Parse(ReadFile(Path.Join(root, CorpusSlice.RegisterPath)));

        IReadOnlyList<ProgramResult> results = CorpusSlice.Run(seeds, oracle);
        SliceVerdict verdict = CorpusSlice.Judge(results, known);
        IReadOnlyList<string> problems = known.Problems(register);

        stdout.WriteLine($"{results.Count} programs: " + string.Join(", ",
            results.GroupBy(r => r.Outcome).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")));
        foreach (ProgramResult result in results) {
            foreach (Finding finding in result.Findings) {
                WriteFinding(result.ProgramId, finding, known.RowFor(result.ProgramId, finding.Signature), stdout);
            }
        }
        if (CorpusSlice.TimedOut(results)) stdout.WriteLine(StoppedAfterTimeout);
        foreach (string stale in verdict.Stale) stdout.WriteLine($"stale    {stale}");
        foreach (string problem in problems) stdout.WriteLine($"problem  {problem}");

        // A slice stopped at a timeout, known or not, left programs unrun and unjudged.
        bool stopped = CorpusSlice.TimedOut(results);
        return verdict.Unknown.Count + verdict.Stale.Count + problems.Count == 0 && !stopped ? 0 : 1;
    }

    private static int Campaign(CampaignOptions options, TextWriter stdout, TextWriter stderr, string workingDirectory, CampaignServices services) {
        // The runner starts the CLI in each script's own directory, so a relative path would not resolve.
        string? grob = options.Grob is null ? null : Path.GetFullPath(options.Grob, workingDirectory);
        if (grob is not null && !File.Exists(grob)) {
            stderr.WriteLine($"error: cannot find {grob}");
            return 3;
        }

        string root = RootOf(options.Root, workingDirectory);
        string knownPath = Path.Join(root, CorpusSlice.KnownFindingsPath);
        KnownFindings? known = File.Exists(knownPath) ? KnownFindings.Parse(ReadFile(knownPath)) : null;
        var supervisor = new CampaignSupervisor(
            services.Launcher(root),
            () => SeedCorpus.Load(root),
            grob is null ? null : services.Grob(grob),
            known, stdout, stderr);
        return supervisor.Run(Path.GetFullPath(options.Out, workingDirectory), new CampaignSettings(options.Seed, options.Programs, options.Workers));
    }

    private static int Worker(WorkerOptions options, TextWriter stderr, string workingDirectory) {
        var directory = new CampaignDirectory(Path.GetFullPath(options.Out, workingDirectory));
        CampaignSettings? settings = directory.ReadSettings();
        if (settings is null) {
            stderr.WriteLine($"error: {options.Out} holds no campaign");
            return 3;
        }
        if (options.Shard >= settings.Workers) {
            stderr.WriteLine($"error: shard {options.Shard} is outside a campaign of {settings.Workers} workers");
            return 3;
        }

        // The corpus is loaded only when the shard reaches its first mutant.
        string root = RootOf(options.Root, workingDirectory);
        var corpus = new Lazy<IReadOnlyDictionary<string, Seed>>(() => SeedCorpus.Load(root).ToDictionary(s => s.Id, StringComparer.Ordinal));
        string Write(PlannedProgram program) => CampaignPlan.Write(program, settings.Seed,
            program.Kind == CampaignPlan.GeneratedKind ? new Dictionary<string, Seed>() : corpus.Value);
        return new CampaignWorker(directory, options.Shard, Write, OracleBudget.Long, OracleBudget.Minimise).Run();
    }

    // A root given on the command line resolves against the working directory, as --out and
    // --grob do; with none, the repository holding the working directory is the root.
    private static string RootOf(string? root, string workingDirectory) =>
        root is null ? SeedCorpus.LocateRepositoryRoot(workingDirectory) : Path.GetFullPath(root, workingDirectory);

    private static void WriteFinding(string program, Finding finding, string? row, TextWriter stdout) {
        stdout.WriteLine(row is null
            ? $"new      {program}  {finding.Signature.Key}  {finding.Message}"
            : $"known    {row}  {program}  {finding.Signature.Key}");
        WriteFrame(finding, stdout);
    }

    private static void WriteFrame(Finding finding, TextWriter stdout) {
        if (finding.FirstFrame is not null) {
            stdout.WriteLine($"    at {finding.FirstFrame}");
        }
    }

    // A read failure is a usage error: the caller named a path the tool cannot use.
    private static string ReadFile(string path) {
        try {
            return File.ReadAllText(path);
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            throw new IOException($"cannot read {path}: {ex.Message}", ex);
        }
    }
}
