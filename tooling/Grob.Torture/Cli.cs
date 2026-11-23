namespace Grob.Torture;

/// <summary>
/// The command line. <c>check &lt;file&gt;...</c> runs each file through the oracle at the CI
/// budget and prints its outcome and findings. Exit codes follow Grob's documented set: <c>0</c>
/// when no run made a finding, <c>1</c> when one did, <c>3</c> for a usage error, including a
/// file that cannot be read.
/// </summary>
internal static class Cli {
    private const string Usage = "usage: grob-torture check <file>...";

    /// <summary>Runs one command and returns the process exit code.</summary>
    public static int Run(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr) {
        if (args.Count < 2 || args[0] != "check") {
            stderr.WriteLine(Usage);
            return 3;
        }
        return Check(args.Skip(1), stdout, stderr);
    }

    private static int Check(IEnumerable<string> paths, TextWriter stdout, TextWriter stderr) {
        var oracle = new Oracle(OracleBudget.Ci);
        bool found = false;
        foreach (string path in paths) {
            string source;
            try {
                source = File.ReadAllText(path);
            } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                stderr.WriteLine($"error: cannot read {path}: {ex.Message}");
                return 3;
            }
            OracleResult result = oracle.Run(source, path);
            Report(path, result, stdout);
            found |= result.Outcome == OutcomeKind.Finding;
        }
        return found ? 1 : 0;
    }

    private static void Report(string path, OracleResult result, TextWriter stdout) {
        stdout.WriteLine($"{path}: {result.Outcome}");
        foreach (Finding finding in result.Findings) {
            stdout.WriteLine($"  {finding.Signature.Key}  {finding.Message}");
            if (finding.FirstFrame is not null) {
                stdout.WriteLine($"    at {finding.FirstFrame}");
            }
        }
    }
}
