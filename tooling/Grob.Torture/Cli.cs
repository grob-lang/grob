using Grob.Integration.Tests.ErrorExamples;

namespace Grob.Torture;

/// <summary>
/// The command line. Exit codes follow Grob's documented set: <c>0</c> when nothing needs
/// triage, <c>1</c> when something does, <c>3</c> for a usage error, including a file or
/// repository that cannot be read.
/// <list type="bullet">
///   <item><description><c>check &lt;file&gt;...</c> runs each file through the oracle at the CI budget and prints its outcome and findings.</description></item>
///   <item><description><c>corpus [&lt;root&gt;]</c> runs the CI slice by hand and lists every finding as known or new, every stale entry and every problem with the known-findings list. The root defaults to the repository holding the working directory.</description></item>
/// </list>
/// </summary>
internal static class Cli {
    private const string Usage = "usage: grob-torture check <file>... | corpus [<root>]";

    /// <summary>Runs one command and returns the process exit code.</summary>
    /// <param name="args">The command and its arguments.</param>
    /// <param name="stdout">Where the report goes.</param>
    /// <param name="stderr">Where usage and read errors go.</param>
    /// <param name="workingDirectory">The directory <c>corpus</c> searches upward from when no root is given.</param>
    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr, string workingDirectory) {
        try {
            return args switch {
                ["check", _, ..] => Check(args.Skip(1), stdout),
                ["corpus"] => Corpus(SeedCorpus.LocateRepositoryRoot(workingDirectory), stdout),
                ["corpus", string root] => Corpus(root, stdout),
                _ => UsageError(stderr),
            };
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            stderr.WriteLine($"error: {ex.Message}");
            return 3;
        }
    }

    private static int UsageError(TextWriter stderr) {
        stderr.WriteLine(Usage);
        return 3;
    }

    private static int Check(IEnumerable<string> paths, TextWriter stdout) {
        var oracle = new Oracle(OracleBudget.Ci);
        bool found = false;
        foreach (string path in paths) {
            string source = ReadFile(path);
            OracleResult result = oracle.Run(source, path);
            stdout.WriteLine($"{path}: {result.Outcome}");
            foreach (Finding finding in result.Findings) {
                stdout.WriteLine($"  {finding.Signature.Key}  {finding.Message}");
                WriteFrame(finding, stdout);
            }
            found |= result.Outcome == OutcomeKind.Finding;
        }
        return found ? 1 : 0;
    }

    private static int Corpus(string root, TextWriter stdout) {
        IReadOnlyList<Seed> seeds = SeedCorpus.Load(root);
        KnownFindings known = KnownFindings.Parse(ReadFile(Path.Join(root, CorpusSlice.KnownFindingsPath)));
        DeferredWorkRegister register = DeferredWorkRegister.Parse(ReadFile(Path.Join(root, CorpusSlice.RegisterPath)));

        IReadOnlyList<ProgramResult> results = CorpusSlice.Run(seeds, new Oracle(OracleBudget.Ci));
        SliceVerdict verdict = CorpusSlice.Judge(results, known);
        IReadOnlyList<string> problems = known.Problems(register);

        stdout.WriteLine($"{results.Count} programs: " + string.Join(", ",
            results.GroupBy(r => r.Outcome).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}")));
        foreach (ProgramResult result in results) {
            foreach (Finding finding in result.Findings) {
                WriteFinding(result.ProgramId, finding, known.RowFor(result.ProgramId, finding.Signature), stdout);
            }
        }
        foreach (string stale in verdict.Stale) stdout.WriteLine($"stale    {stale}");
        foreach (string problem in problems) stdout.WriteLine($"problem  {problem}");

        return verdict.Unknown.Count + verdict.Stale.Count + problems.Count == 0 ? 0 : 1;
    }

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
