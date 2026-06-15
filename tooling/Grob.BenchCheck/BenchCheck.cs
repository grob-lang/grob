using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grob.BenchCheck;

// --- BenchmarkDotNet -report-full.json (the subset BenchCheck reads) ---

/// <summary>
/// The subset of a BenchmarkDotNet <c>-report-full.json</c> file that
/// BenchCheck reads: the host environment and the benchmark list.
/// </summary>
/// <param name="HostEnvironmentInfo">Machine and runtime metadata for the run.</param>
/// <param name="Benchmarks">All benchmark results in the report.</param>
public sealed record BdnReport(
    [property: JsonPropertyName("HostEnvironmentInfo")] BdnHostEnvironmentInfo? HostEnvironmentInfo,
    [property: JsonPropertyName("Benchmarks")] IReadOnlyList<BdnBenchmark>? Benchmarks);

/// <summary>
/// Machine and runtime metadata from a BenchmarkDotNet report.
/// </summary>
/// <param name="OsVersion">Operating-system version string (e.g. <c>Windows 10.0.22621</c>).</param>
/// <param name="ProcessorName">CPU model name.</param>
/// <param name="RuntimeVersion">.NET runtime version string.</param>
public sealed record BdnHostEnvironmentInfo(
    [property: JsonPropertyName("OsVersion")] string? OsVersion,
    [property: JsonPropertyName("ProcessorName")] string? ProcessorName,
    [property: JsonPropertyName("RuntimeVersion")] string? RuntimeVersion);

/// <summary>
/// A single benchmark result entry from a BenchmarkDotNet report.
/// </summary>
/// <param name="FullName">Fully qualified benchmark method name (namespace + class + method).</param>
/// <param name="Statistics">Timing statistics for this benchmark.</param>
public sealed record BdnBenchmark(
    [property: JsonPropertyName("FullName")] string? FullName,
    [property: JsonPropertyName("Statistics")] BdnStatistics? Statistics);

/// <summary>
/// Timing statistics for a single benchmark from a BenchmarkDotNet report.
/// </summary>
/// <param name="Mean">Arithmetic mean execution time in nanoseconds.</param>
public sealed record BdnStatistics(
    [property: JsonPropertyName("Mean")] double Mean);

// --- policy.json ---

/// <summary>
/// Benchmark regression policy loaded from <c>policy.json</c>. Defines the
/// per-sprint and cumulative thresholds and the list of benchmark categories.
/// </summary>
/// <param name="PerSprintPercent">
/// Maximum allowed percentage increase in mean execution time relative to the
/// rolling baseline before a gating category is declared a breach.
/// </param>
/// <param name="CumulativePercent">
/// Maximum allowed percentage increase relative to the frozen origin baseline
/// across all sprints.
/// </param>
/// <param name="Categories">The benchmark categories to evaluate.</param>
public sealed record Policy(
    [property: JsonPropertyName("perSprintPercent")] double PerSprintPercent,
    [property: JsonPropertyName("cumulativePercent")] double CumulativePercent,
    [property: JsonPropertyName("categories")] IReadOnlyList<PolicyCategory> Categories);

/// <summary>
/// A single benchmark category entry in <c>policy.json</c>.
/// </summary>
/// <param name="Name">Human-readable category label used in reports.</param>
/// <param name="NamespacePrefix">
/// Benchmark <see cref="BdnBenchmark.FullName"/> must start with this prefix
/// to be counted in this category.
/// </param>
/// <param name="Baseline">
/// Filename of the rolling baseline JSON file (relative to the baseline
/// directory, e.g. <c>compile.json</c>).
/// </param>
/// <param name="Gating">
/// When <see langword="true"/>, a breach in this category fails the gate.
/// When <see langword="false"/>, results are reported but never fail.
/// </param>
public sealed record PolicyCategory(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("namespacePrefix")] string NamespacePrefix,
    [property: JsonPropertyName("baseline")] string Baseline,
    [property: JsonPropertyName("gating")] bool Gating);

// --- evaluation model ---

/// <summary>
/// Classification of a single benchmark's comparison result.
/// </summary>
public enum DeltaClass {
    /// <summary>Within both thresholds; no action needed.</summary>
    Ok,
    /// <summary>Non-gating category — reported for information, never fails the gate.</summary>
    Informational,
    /// <summary>Present in the fresh run but absent from the rolling baseline; treated as informational.</summary>
    NewBenchmark,
    /// <summary>The rolling baseline file for this category does not exist yet; establishing.</summary>
    NoBaseline,
    /// <summary>Fresh mean exceeds the rolling baseline by more than <see cref="Policy.PerSprintPercent"/>.</summary>
    PerSprintBreach,
    /// <summary>Fresh mean exceeds the origin baseline by more than <see cref="Policy.CumulativePercent"/>.</summary>
    CumulativeBreach,
    /// <summary>Fresh run and baseline were measured on different OS families; comparison refused.</summary>
    RunnerMismatch,
}

/// <summary>
/// Comparison result for a single benchmark against both baseline axes.
/// </summary>
/// <param name="Category">Name of the policy category this benchmark belongs to.</param>
/// <param name="FullName">Fully qualified benchmark method name.</param>
/// <param name="PerSprintPercent">
/// Percentage change relative to the rolling baseline, or <see langword="null"/>
/// when the benchmark is new (not present in the rolling baseline).
/// </param>
/// <param name="CumulativePercent">
/// Percentage change relative to the frozen origin baseline, or <see langword="null"/>
/// when the origin baseline is absent.
/// </param>
/// <param name="Class">Classification of this result.</param>
public sealed record BenchmarkDelta(
    string Category,
    string FullName,
    double? PerSprintPercent,
    double? CumulativePercent,
    DeltaClass Class);

/// <summary>
/// Overall outcome of a benchmark gate evaluation run.
/// </summary>
public enum Outcome {
    /// <summary>All gating benchmarks are within threshold.</summary>
    Pass,
    /// <summary>At least one gating benchmark exceeds a threshold.</summary>
    Regression,
    /// <summary>Runner type mismatch made comparison impossible.</summary>
    CannotCompare,
}

/// <summary>
/// Full result of a gate evaluation: the outcome, per-benchmark deltas, and
/// informational notes.
/// </summary>
/// <param name="Outcome">Overall pass/regression/cannot-compare verdict.</param>
/// <param name="Deltas">Per-benchmark comparison results.</param>
/// <param name="Notes">Informational messages (missing baselines, runner info, etc.).</param>
public sealed record EvaluationReport(
    Outcome Outcome,
    IReadOnlyList<BenchmarkDelta> Deltas,
    IReadOnlyList<string> Notes);

/// <summary>
/// A single side of a comparison: the per-benchmark means and the host they were measured on.
/// </summary>
/// <param name="Host">Machine and runtime metadata, or <see langword="null"/> if not available.</param>
/// <param name="Means">Map of fully qualified benchmark name to mean nanoseconds.</param>
public sealed record BaselineSide(
    BdnHostEnvironmentInfo? Host,
    IReadOnlyDictionary<string, double> Means);

/// <summary>
/// Core logic for the benchmark regression gate (D-313). All methods are pure
/// or thin file-IO wrappers so the gate logic is unit-testable with in-memory inputs.
/// </summary>
public static class BenchCheck {
    /// <summary>
    /// Shared <see cref="JsonSerializerOptions"/> used when reading BenchmarkDotNet
    /// reports and policy files. Case-insensitive, allows comments and trailing commas.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new() {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Pure evaluation — no file I/O. Computes a <see cref="EvaluationReport"/> by
    /// comparing <paramref name="fresh"/> against the rolling and origin baselines for
    /// each category in <paramref name="policy"/>.
    /// </summary>
    /// <param name="policy">The gate policy (thresholds and category list).</param>
    /// <param name="fresh">The freshly measured benchmark results to evaluate.</param>
    /// <param name="loadBaseline">
    /// Callback that loads a baseline side by filename (relative to the baseline
    /// directory). Returns <see langword="null"/> when the file does not exist.
    /// </param>
    /// <returns>The full evaluation report including outcome, deltas and notes.</returns>
    public static EvaluationReport Evaluate(
        Policy policy,
        BaselineSide fresh,
        Func<string, BaselineSide?> loadBaseline) {
        var deltas = new List<BenchmarkDelta>();
        var notes = new List<string>();
        var regression = false;
        var cannotCompare = false;

        foreach (var category in policy.Categories) {
            var freshInCategory = fresh.Means
                .Where(kv => kv.Key.StartsWith(category.NamespacePrefix, StringComparison.Ordinal))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToList();

            if (freshInCategory.Count == 0) {
                notes.Add($"{category.Name}: no fresh benchmarks matched '{category.NamespacePrefix}' — nothing to compare.");
                continue;
            }

            var rolling = loadBaseline(category.Baseline);
            if (rolling is null) {
                foreach (var (name, _) in freshInCategory)
                    deltas.Add(new BenchmarkDelta(category.Name, name, null, null, DeltaClass.NoBaseline));
                notes.Add($"{category.Name}: rolling baseline '{category.Baseline}' not found — establishing, no comparison.");
                continue;
            }

            // Runner guard. A cross-runner comparison is meaningless (D-309 / §9); on a
            // gating category that is a hard "cannot compare", not a silent green.
            if (category.Gating && !SameRunnerType(fresh.Host, rolling.Host)) {
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

            foreach (var (name, freshMean) in freshInCategory) {
                double? perSprint = rolling.Means.TryGetValue(name, out var rMean) ? Percent(freshMean, rMean) : null;
                double? cumulative = origin is not null && origin.Means.TryGetValue(name, out var oMean) ? Percent(freshMean, oMean) : null;

                DeltaClass cls;
                if (perSprint is null) {
                    cls = DeltaClass.NewBenchmark; // new benchmark this sprint — informational
                } else if (!category.Gating) {
                    cls = DeltaClass.Informational;
                } else if (perSprint.Value > policy.PerSprintPercent) {
                    cls = DeltaClass.PerSprintBreach;
                    regression = true;
                } else if (cumulative is not null && cumulative.Value > policy.CumulativePercent) {
                    cls = DeltaClass.CumulativeBreach;
                    regression = true;
                } else {
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

    /// <summary>
    /// Computes the percentage change of <paramref name="fresh"/> relative to
    /// <paramref name="baseline"/>. A positive value means the fresh run is slower.
    /// Returns <c>0</c> when <paramref name="baseline"/> is zero to avoid division by zero.
    /// </summary>
    /// <param name="fresh">The new mean nanoseconds.</param>
    /// <param name="baseline">The reference mean nanoseconds.</param>
    /// <returns>Signed percentage change, e.g. <c>+5.0</c> for 5% slower.</returns>
    public static double Percent(double fresh, double baseline)
        => baseline == 0 ? 0 : (fresh - baseline) / baseline * 100.0;

    /// <summary>
    /// Derives the origin baseline filename from the rolling baseline filename by
    /// inserting <c>.origin</c> before the extension (e.g. <c>compile.json</c> →
    /// <c>compile.origin.json</c>).
    /// </summary>
    /// <param name="baselineFileName">The rolling baseline filename.</param>
    /// <returns>The corresponding origin baseline filename.</returns>
    public static string OriginName(string baselineFileName) {
        var ext = Path.GetExtension(baselineFileName);
        var stem = Path.GetFileNameWithoutExtension(baselineFileName);
        return $"{stem}.origin{ext}";
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="a"/> and <paramref name="b"/>
    /// ran on the same OS family (windows / macos / linux). GitHub-hosted runners vary
    /// CPU generation run-to-run; the per-sprint gate absorbs that variance. This guard
    /// exists solely to refuse windows-vs-linux comparisons.
    /// </summary>
    /// <param name="a">First host, or <see langword="null"/> if unavailable.</param>
    /// <param name="b">Second host, or <see langword="null"/> if unavailable.</param>
    public static bool SameRunnerType(BdnHostEnvironmentInfo? a, BdnHostEnvironmentInfo? b)
        => PlatformOf(a) == PlatformOf(b) && PlatformOf(a) != "unknown";

    /// <summary>
    /// Classifies a host into one of three OS family strings: <c>"windows"</c>,
    /// <c>"macos"</c>, <c>"linux"</c>, or <c>"unknown"</c> when the OS string is
    /// absent or unrecognised.
    /// </summary>
    /// <param name="host">Host environment info, or <see langword="null"/>.</param>
    /// <returns>Lowercase OS family string.</returns>
    public static string PlatformOf(BdnHostEnvironmentInfo? host) {
        var os = host?.OsVersion?.ToLowerInvariant() ?? string.Empty;
        if (os.Contains("windows")) return "windows";
        if (os.Contains("macos") || os.Contains("os x") || os.Contains("darwin")) return "macos";
        if (os.Contains("ubuntu") || os.Contains("linux") || os.Contains("unix")) return "linux";
        return "unknown";
    }

    // --- file IO wrappers (thin; the logic above is the tested part) ---

    /// <summary>
    /// Reads and deserialises a <c>policy.json</c> file.
    /// </summary>
    /// <param name="path">Path to the policy JSON file.</param>
    /// <returns>The deserialised <see cref="Policy"/>.</returns>
    /// <exception cref="InvalidDataException">The file could not be parsed as a valid policy.</exception>
    public static Policy LoadPolicy(string path)
        => JsonSerializer.Deserialize<Policy>(File.ReadAllText(path), Json)
           ?? throw new InvalidDataException($"Could not parse policy file '{path}'.");

    /// <summary>
    /// Reads and deserialises a BenchmarkDotNet <c>-report-full.json</c> file.
    /// </summary>
    /// <param name="path">Path to the report JSON file.</param>
    /// <returns>The deserialised <see cref="BdnReport"/>.</returns>
    /// <exception cref="InvalidDataException">The file could not be parsed as a valid report.</exception>
    public static BdnReport LoadReport(string path)
        => JsonSerializer.Deserialize<BdnReport>(File.ReadAllText(path), Json)
           ?? throw new InvalidDataException($"Could not parse report file '{path}'.");

    /// <summary>
    /// Converts a <see cref="BdnReport"/> to a <see cref="BaselineSide"/> by extracting
    /// the mean nanoseconds for each benchmark.
    /// </summary>
    /// <param name="report">The report to convert.</param>
    /// <returns>
    /// A <see cref="BaselineSide"/> whose <see cref="BaselineSide.Means"/> map contains
    /// every benchmark with a non-empty name and non-null statistics.
    /// </returns>
    public static BaselineSide ToSide(BdnReport report) {
        var means = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var b in report.Benchmarks ?? []) {
            if (b.FullName is { Length: > 0 } name && b.Statistics is { } stats)
                means[name] = stats.Mean;
        }
        return new BaselineSide(report.HostEnvironmentInfo, means);
    }

    /// <summary>
    /// Merges every <c>*-report-full.json</c> found (recursively) under
    /// <paramref name="resultsDir"/> into a single <see cref="BaselineSide"/>.
    /// The host is taken from the first file; later files' benchmark means overwrite
    /// earlier ones if names collide.
    /// </summary>
    /// <param name="resultsDir">Directory containing BenchmarkDotNet result files.</param>
    /// <returns>The merged fresh side.</returns>
    /// <exception cref="FileNotFoundException">
    /// No <c>*-report-full.json</c> files were found under <paramref name="resultsDir"/>.
    /// </exception>
    public static BaselineSide CollectFresh(string resultsDir) {
        var files = Directory.EnumerateFiles(resultsDir, "*-report-full.json", SearchOption.AllDirectories).ToList();
        if (files.Count == 0)
            throw new FileNotFoundException($"No '*-report-full.json' found under '{resultsDir}'.");

        var means = new Dictionary<string, double>(StringComparer.Ordinal);
        BdnHostEnvironmentInfo? host = null;
        foreach (var file in files) {
            var report = LoadReport(file);
            host ??= report.HostEnvironmentInfo;
            foreach (var (k, v) in ToSide(report).Means)
                means[k] = v;
        }
        return new BaselineSide(host, means);
    }

    /// <summary>
    /// Returns a callback that loads a committed baseline file from
    /// <paramref name="baselineDir"/> by filename, or returns <see langword="null"/>
    /// when the file does not exist. Used as the <c>loadBaseline</c> argument to
    /// <see cref="Evaluate"/>.
    /// </summary>
    /// <param name="baselineDir">Directory containing committed baseline JSON files.</param>
    /// <returns>A function mapping a baseline filename to its <see cref="BaselineSide"/>, or <see langword="null"/>.</returns>
    public static Func<string, BaselineSide?> BaselineLoader(string baselineDir)
        => fileName => {
            var path = Path.Combine(baselineDir, fileName);
            return File.Exists(path) ? ToSide(LoadReport(path)) : null;
        };
}
