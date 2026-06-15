using System.Globalization;
using System.Text;
using Grob.BenchCheck;

// Grob.BenchCheck — the benchmark regression gate (D-313).
//
// Reads the committed baselines and a fresh BenchmarkDotNet run, computes the
// two-axis comparison (per-sprint vs rolling, cumulative vs frozen origin),
// writes a delta table to stdout and to the GitHub job summary, and sets the
// exit code so the workflow goes red on a breach.
//
//   exit 0  pass
//   exit 1  regression breach (per-sprint or cumulative, on a gating category)
//   exit 2  cannot compare / usage / IO error

return Cli.Run(args);

internal static class Cli
{
    public static int Run(string[] args)
    {
        try
        {
            var opts = Options.Parse(args);

            var policy = BenchCheck.LoadPolicy(opts.PolicyPath);
            var fresh = BenchCheck.CollectFresh(opts.ResultsDir);
            var report = BenchCheck.Evaluate(policy, fresh, BenchCheck.BaselineLoader(opts.BaselineDir));

            var rendered = Render(policy, fresh, report);
            Console.WriteLine(rendered);
            if (opts.SummaryPath is { Length: > 0 } summary)
                File.AppendAllText(summary, rendered + Environment.NewLine);

            return report.Outcome switch
            {
                Outcome.Pass => 0,
                Outcome.Regression => 1,
                _ => 2,
            };
        }
        catch (Exception ex) when (ex is OptionsException or FileNotFoundException or InvalidDataException or DirectoryNotFoundException)
        {
            Console.Error.WriteLine($"BenchCheck: {ex.Message}");
            return 2;
        }
    }

    private static string Render(Policy policy, BaselineSide fresh, EvaluationReport report)
    {
        static string Pct(double? v) => v is null ? "—" : v.Value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";
        static string Short(string fullName)
        {
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
        }

        var verdict = report.Outcome switch
        {
            Outcome.Pass => "PASS",
            Outcome.Regression => "REGRESSION",
            _ => "CANNOT COMPARE",
        };

        var sb = new StringBuilder();
        sb.AppendLine($"## Benchmark regression gate — {verdict}");
        sb.AppendLine();
        sb.AppendLine($"Runner: {BenchCheck.PlatformOf(fresh.Host)} · {fresh.Host?.ProcessorName ?? "unknown CPU"} · {fresh.Host?.RuntimeVersion ?? "unknown runtime"}");
        sb.AppendLine($"Thresholds: per-sprint {policy.PerSprintPercent:0.#}% (vs rolling) · cumulative {policy.CumulativePercent:0.#}% (vs origin)");
        sb.AppendLine();
        sb.AppendLine("| Category | Benchmark | vs rolling | vs origin | Status |");
        sb.AppendLine("| --- | --- | ---: | ---: | --- |");
        foreach (var d in report.Deltas)
        {
            var status = d.Class switch
            {
                DeltaClass.Ok => "ok",
                DeltaClass.Informational => "info",
                DeltaClass.NewBenchmark => "new",
                DeltaClass.NoBaseline => "establishing",
                DeltaClass.PerSprintBreach => "**per-sprint breach**",
                DeltaClass.CumulativeBreach => "**cumulative breach**",
                DeltaClass.RunnerMismatch => "**runner mismatch**",
                _ => d.Class.ToString(),
            };
            sb.AppendLine($"| {d.Category} | {Short(d.FullName)} | {Pct(d.PerSprintPercent)} | {Pct(d.CumulativePercent)} | {status} |");
        }

        if (report.Notes.Count > 0)
        {
            sb.AppendLine();
            foreach (var note in report.Notes)
                sb.AppendLine($"> {note}");
        }

        return sb.ToString().TrimEnd();
    }
}

internal sealed record Options(string ResultsDir, string BaselineDir, string PolicyPath, string? SummaryPath)
{
    public static Options Parse(string[] args)
    {
        var results = "BenchmarkDotNet.Artifacts/results";
        var baseline = "bench/Grob.Benchmarks/baseline";
        string? policy = null;
        string? summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--results": results = Next(args, ref i); break;
                case "--baseline": baseline = Next(args, ref i); break;
                case "--policy": policy = Next(args, ref i); break;
                case "--summary": summary = Next(args, ref i); break;
                case "-h" or "--help": throw new OptionsException(Usage);
                default: throw new OptionsException($"Unknown argument '{args[i]}'.{Environment.NewLine}{Usage}");
            }
        }

        policy ??= Path.Combine(baseline, "policy.json");
        return new Options(results, baseline, policy, summary);
    }

    private static string Next(string[] args, ref int i)
        => ++i < args.Length ? args[i] : throw new OptionsException($"Missing value for '{args[i - 1]}'.");

    public const string Usage =
        "Usage: Grob.BenchCheck [--results <dir>] [--baseline <dir>] [--policy <file>] [--summary <file>]";
}

internal sealed class OptionsException(string message) : Exception(message);
