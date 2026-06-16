using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Grob.BenchCheck;

internal static class Cli {
    [ExcludeFromCodeCoverage(Justification = "I/O sink: writes to Console and File. Logic is in BenchCheck.Evaluate (unit-tested) and Cli.Render (unit-tested).")]
    public static int Run(string[] args) {
        try {
            var opts = Options.Parse(args);

            var policy = BenchCheck.LoadPolicy(opts.PolicyPath);
            var fresh = BenchCheck.CollectFresh(opts.ResultsDir);
            var report = BenchCheck.Evaluate(policy, fresh, BenchCheck.BaselineLoader(opts.BaselineDir));

            var rendered = Render(policy, fresh, report);
            Console.WriteLine(rendered);
            if (opts.SummaryPath is { Length: > 0 } summary)
                File.AppendAllText(summary, rendered + Environment.NewLine);

            return report.Outcome switch {
                Outcome.Pass => 0,
                Outcome.Regression => 1,
                _ => 2,
            };
        } catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or DirectoryNotFoundException) {
            Console.Error.WriteLine($"BenchCheck: {ex.Message}");
            return 2;
        }
    }

    internal static string Render(Policy policy, BaselineSide fresh, EvaluationReport report) {
        static string Pct(double? v) => v is null ? "—" : v.Value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";
        static string Short(string fullName) {
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
        }

        var verdict = report.Outcome switch {
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
        foreach (var d in report.Deltas) {
            var status = d.Class switch {
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

        if (report.Notes.Count > 0) {
            sb.AppendLine();
            foreach (var note in report.Notes)
                sb.AppendLine($"> {note}");
        }

        return sb.ToString().TrimEnd();
    }
}
