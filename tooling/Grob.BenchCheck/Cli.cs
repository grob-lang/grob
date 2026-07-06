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
                _ => 1,
            };
        } catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or DirectoryNotFoundException) {
            Console.Error.WriteLine($"BenchCheck: {ex.Message}");
            return 2;
        }
    }

    internal static string Render(Policy policy, BaselineSide fresh, EvaluationReport report) {
        static string Pct(double? v) => v is null ? "—" : v.Value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture) + "%";
        static string Bytes(double? v) => v is null ? "—" : v.Value.ToString("N0", CultureInfo.InvariantCulture) + " B";
        static string Short(string fullName) {
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
        }
        static string TimeStatus(TimeClass cls) => cls switch {
            TimeClass.Ok => "ok",
            TimeClass.Informational => "info",
            TimeClass.CpuMismatch => "cpu mismatch",
            TimeClass.NewBenchmark => "new",
            TimeClass.NoBaseline => "establishing",
            TimeClass.PerSprintBreach => "**per-sprint breach**",
            TimeClass.CumulativeBreach => "**cumulative breach**",
            _ => cls.ToString(),
        };
        static string AllocStatus(AllocClass cls) => cls switch {
            AllocClass.Ok => "ok",
            AllocClass.Informational => "info",
            AllocClass.NewBenchmark => "new",
            AllocClass.NoBaseline => "establishing",
            AllocClass.PerSprintBreach => "**per-sprint breach**",
            AllocClass.LohTripwireBreach => "**LOH tripwire**",
            _ => cls.ToString(),
        };

        var verdict = report.Outcome == Outcome.Pass ? "PASS" : "REGRESSION";

        var sb = new StringBuilder();
        sb.AppendLine($"## Benchmark regression gate — {verdict}");
        sb.AppendLine();
        sb.AppendLine($"Runner: {fresh.Host?.OsVersion ?? "unknown OS"} · {fresh.Host?.ProcessorName ?? "unknown CPU"} · {fresh.Host?.RuntimeVersion ?? "unknown runtime"}");
        sb.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Thresholds: per-sprint {policy.PerSprintPercent:0.#}% (vs rolling, noise floor ×{policy.TimeSignificanceK:0.#}σ) · cumulative {policy.CumulativePercent:0.#}% (vs origin) · allocation {policy.AllocPercent:0.#}% · LOH tripwire {policy.LohTripwireBytes:N0} B"));
        sb.AppendLine();
        sb.AppendLine("| Category | Benchmark | Δ time (rolling) | Δ time (origin) | Time | Δ alloc | Alloc | Alloc status |");
        sb.AppendLine("| --- | --- | ---: | ---: | --- | ---: | ---: | --- |");
        foreach (var d in report.Deltas) {
            sb.AppendLine(
                $"| {d.Category} | {Short(d.FullName)} | {Pct(d.TimePerSprintPercent)} | {Pct(d.TimeCumulativePercent)} | {TimeStatus(d.TimeClass)} | " +
                $"{Pct(d.AllocPercent)} | {Bytes(d.AllocBytes)} | {AllocStatus(d.AllocClass)} |");
        }

        if (report.Notes.Count > 0) {
            sb.AppendLine();
            foreach (var note in report.Notes)
                sb.AppendLine($"> {note}");
        }

        return sb.ToString().TrimEnd();
    }
}
