using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grob.BenchCheck;

// --- BenchmarkDotNet -report-full.json (the subset BenchCheck reads) ---

public sealed record BdnReport(
    [property: JsonPropertyName("HostEnvironmentInfo")] BdnHostEnvironmentInfo? HostEnvironmentInfo,
    [property: JsonPropertyName("Benchmarks")] IReadOnlyList<BdnBenchmark>? Benchmarks);

public sealed record BdnHostEnvironmentInfo(
    [property: JsonPropertyName("OsVersion")] string? OsVersion,
    [property: JsonPropertyName("ProcessorName")] string? ProcessorName,
    [property: JsonPropertyName("RuntimeVersion")] string? RuntimeVersion);

public sealed record BdnBenchmark(
    [property: JsonPropertyName("FullName")] string? FullName,
    [property: JsonPropertyName("Statistics")] BdnStatistics? Statistics);

public sealed record BdnStatistics(
    [property: JsonPropertyName("Mean")] double Mean);

// --- policy.json ---

public sealed record Policy(
    [property: JsonPropertyName("perSprintPercent")] double PerSprintPercent,
    [property: JsonPropertyName("cumulativePercent")] double CumulativePercent,
    [property: JsonPropertyName("categories")] IReadOnlyList<PolicyCategory> Categories);

public sealed record PolicyCategory(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("namespacePrefix")] string NamespacePrefix,
    [property: JsonPropertyName("baseline")] string Baseline,
    [property: JsonPropertyName("gating")] bool Gating);

// --- evaluation model ---

public enum DeltaClass
{
    Ok,
    Informational,      // non-gating category — reported, never fails
    NewBenchmark,       // present in the fresh run, absent from the baseline
    NoBaseline,         // category's rolling baseline not yet established
    PerSprintBreach,    // gating: fresh exceeds rolling by > perSprintPercent
    CumulativeBreach,   // gating: fresh exceeds origin by > cumulativePercent
    RunnerMismatch,     // gating: fresh and baseline ran on different runner types
}

public sealed record BenchmarkDelta(
    string Category,
    string FullName,
    double? PerSprintPercent,
    double? CumulativePercent,
    DeltaClass Class);

public enum Outcome { Pass, Regression, CannotCompare }

public sealed record EvaluationReport(
    Outcome Outcome,
    IReadOnlyList<BenchmarkDelta> Deltas,
    IReadOnlyList<string> Notes);

/// A single side of a comparison: the per-benchmark means and the host they were measured on.
public sealed record BaselineSide(
    BdnHostEnvironmentInfo? Host,
    IReadOnlyDictionary<string, double> Means);

public static class BenchCheck
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// Pure evaluation. `loadBaseline` returns the rolling/origin side for a baseline
    /// filename, or null when that file is absent. No file IO lives here so the gate
    /// logic is unit-testable with in-memory inputs.
    public static EvaluationReport Evaluate(
        Policy policy,
        BaselineSide fresh,
        Func<string, BaselineSide?> loadBaseline)
    {
        var deltas = new List<BenchmarkDelta>();
        var notes = new List<string>();
        var regression = false;
        var cannotCompare = false;

        foreach (var category in policy.Categories)
        {
            var freshInCategory = fresh.Means
                .Where(kv => kv.Key.StartsWith(category.NamespacePrefix, StringComparison.Ordinal))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToList();

            if (freshInCategory.Count == 0)
            {
                notes.Add($"{category.Name}: no fresh benchmarks matched '{category.NamespacePrefix}' — nothing to compare.");
                continue;
            }

            var rolling = loadBaseline(category.Baseline);
            if (rolling is null)
            {
                foreach (var (name, _) in freshInCategory)
                    deltas.Add(new BenchmarkDelta(category.Name, name, null, null, DeltaClass.NoBaseline));
                notes.Add($"{category.Name}: rolling baseline '{category.Baseline}' not found — establishing, no comparison.");
                continue;
            }

            // Runner guard. A cross-runner comparison is meaningless (D-309 / §9); on a
            // gating category that is a hard "cannot compare", not a silent green.
            if (category.Gating && !SameRunnerType(fresh.Host, rolling.Host))
            {
                foreach (var (name, _) in freshInCategory)
                    deltas.Add(new BenchmarkDelta(category.Name, name, null, null, DeltaClass.RunnerMismatch));
                notes.Add(
                    $"{category.Name}: runner mismatch — fresh '{PlatformOf(fresh.Host)}' vs baseline " +
                    $"'{PlatformOf(rolling.Host)}'. Cross-runner comparison refused.");
                cannotCompare = true;
                continue;
            }

            var origin = loadBaseline(OriginName(category.Baseline));
            if (origin is null)
                notes.Add($"{category.Name}: origin baseline '{OriginName(category.Baseline)}' not found — cumulative axis skipped.");

            foreach (var (name, freshMean) in freshInCategory)
            {
                double? perSprint = rolling.Means.TryGetValue(name, out var rMean) ? Percent(freshMean, rMean) : null;
                double? cumulative = origin is not null && origin.Means.TryGetValue(name, out var oMean) ? Percent(freshMean, oMean) : null;

                DeltaClass cls;
                if (perSprint is null)
                {
                    cls = DeltaClass.NewBenchmark; // new benchmark this sprint — informational
                }
                else if (!category.Gating)
                {
                    cls = DeltaClass.Informational;
                }
                else if (perSprint.Value > policy.PerSprintPercent)
                {
                    cls = DeltaClass.PerSprintBreach;
                    regression = true;
                }
                else if (cumulative is not null && cumulative.Value > policy.CumulativePercent)
                {
                    cls = DeltaClass.CumulativeBreach;
                    regression = true;
                }
                else
                {
                    cls = DeltaClass.Ok;
                }

                deltas.Add(new BenchmarkDelta(category.Name, name, perSprint, cumulative, cls));
            }
        }

        var outcome = cannotCompare ? Outcome.CannotCompare
            : regression ? Outcome.Regression
            : Outcome.Pass;

        return new EvaluationReport(outcome, deltas, notes);
    }

    /// Positive percent means the fresh run is slower (higher mean) than the baseline.
    public static double Percent(double fresh, double baseline)
        => baseline == 0 ? 0 : (fresh - baseline) / baseline * 100.0;

    public static string OriginName(string baselineFileName)
    {
        var ext = Path.GetExtension(baselineFileName);
        var stem = Path.GetFileNameWithoutExtension(baselineFileName);
        return $"{stem}.origin{ext}";
    }

    /// Same runner *type* — OS family, not exact CPU. GitHub-hosted runners vary CPU
    /// generation run-to-run on the same label; that variance is what the per-sprint
    /// gate absorbs. The guard exists to stop windows-vs-linux comparisons.
    public static bool SameRunnerType(BdnHostEnvironmentInfo? a, BdnHostEnvironmentInfo? b)
        => PlatformOf(a) == PlatformOf(b) && PlatformOf(a) != "unknown";

    public static string PlatformOf(BdnHostEnvironmentInfo? host)
    {
        var os = host?.OsVersion?.ToLowerInvariant() ?? string.Empty;
        if (os.Contains("windows")) return "windows";
        if (os.Contains("macos") || os.Contains("os x") || os.Contains("darwin")) return "macos";
        if (os.Contains("ubuntu") || os.Contains("linux") || os.Contains("unix")) return "linux";
        return "unknown";
    }

    // --- file IO wrappers (thin; the logic above is the tested part) ---

    public static Policy LoadPolicy(string path)
        => JsonSerializer.Deserialize<Policy>(File.ReadAllText(path), Json)
           ?? throw new InvalidDataException($"Could not parse policy file '{path}'.");

    public static BdnReport LoadReport(string path)
        => JsonSerializer.Deserialize<BdnReport>(File.ReadAllText(path), Json)
           ?? throw new InvalidDataException($"Could not parse report file '{path}'.");

    public static BaselineSide ToSide(BdnReport report)
    {
        var means = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var b in report.Benchmarks ?? [])
        {
            if (b.FullName is { Length: > 0 } name && b.Statistics is { } stats)
                means[name] = stats.Mean;
        }
        return new BaselineSide(report.HostEnvironmentInfo, means);
    }

    /// Merge every `*-report-full.json` in the results directory into one fresh side.
    public static BaselineSide CollectFresh(string resultsDir)
    {
        var files = Directory.EnumerateFiles(resultsDir, "*-report-full.json", SearchOption.AllDirectories).ToList();
        if (files.Count == 0)
            throw new FileNotFoundException($"No '*-report-full.json' found under '{resultsDir}'.");

        var means = new Dictionary<string, double>(StringComparer.Ordinal);
        BdnHostEnvironmentInfo? host = null;
        foreach (var file in files)
        {
            var report = LoadReport(file);
            host ??= report.HostEnvironmentInfo;
            foreach (var (k, v) in ToSide(report).Means)
                means[k] = v;
        }
        return new BaselineSide(host, means);
    }

    /// Loads a committed baseline file from the baseline directory, or null if absent.
    public static Func<string, BaselineSide?> BaselineLoader(string baselineDir)
        => fileName =>
        {
            var path = Path.Combine(baselineDir, fileName);
            return File.Exists(path) ? ToSide(LoadReport(path)) : null;
        };
}
