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
/// <param name="ProcessorName">CPU model name. The CPU-identity source of record (D-333).</param>
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
/// <param name="Memory">Allocation statistics for this benchmark, or <see langword="null"/> if <c>[MemoryDiagnoser]</c> was not attached.</param>
public sealed record BdnBenchmark(
    [property: JsonPropertyName("FullName")] string? FullName,
    [property: JsonPropertyName("Statistics")] BdnStatistics? Statistics,
    [property: JsonPropertyName("Memory")] BdnMemory? Memory);

/// <summary>
/// Timing statistics for a single benchmark from a BenchmarkDotNet report.
/// </summary>
/// <param name="Mean">Arithmetic mean execution time in nanoseconds.</param>
/// <param name="StandardDeviation">Standard deviation of execution time in nanoseconds, used as the measurement-noise signal for the significance-aware time gate (D-333).</param>
public sealed record BdnStatistics(
    [property: JsonPropertyName("Mean")] double Mean,
    [property: JsonPropertyName("StandardDeviation")] double StandardDeviation);

/// <summary>
/// Allocation statistics for a single benchmark from a BenchmarkDotNet report
/// (<c>[MemoryDiagnoser]</c> output).
/// </summary>
/// <param name="BytesAllocatedPerOperation">Managed bytes allocated per operation.</param>
public sealed record BdnMemory(
    [property: JsonPropertyName("BytesAllocatedPerOperation")] double? BytesAllocatedPerOperation);

// --- policy.json ---

/// <summary>
/// Benchmark regression policy loaded from <c>policy.json</c>. Defines the
/// per-sprint, cumulative and allocation thresholds, the time-significance
/// factor and the list of benchmark categories.
/// </summary>
/// <param name="PerSprintPercent">
/// Maximum allowed percentage increase in mean execution time relative to the
/// rolling baseline before a gating category is declared a breach.
/// </param>
/// <param name="CumulativePercent">
/// Maximum allowed percentage increase relative to the frozen origin baseline
/// across all sprints.
/// </param>
/// <param name="AllocPercent">
/// Maximum allowed percentage increase in bytes allocated per operation
/// relative to the rolling baseline before a gating category is declared a
/// breach (D-333).
/// </param>
/// <param name="LohTripwireBytes">
/// Absolute bytes-allocated-per-operation ceiling. Any benchmark, gating or
/// not, whose fresh allocation meets or exceeds this fails the gate outright —
/// the deterministic signal that would have caught the D-332 defect on day
/// one (D-333).
/// </param>
/// <param name="TimeSignificanceK">
/// Multiplier applied to a benchmark's relative standard deviation; the
/// per-sprint time breach requires the delta to exceed
/// <c>max(PerSprintPercent, TimeSignificanceK * relativeStdDev)</c> (D-333).
/// </param>
/// <param name="Categories">The benchmark categories to evaluate.</param>
public sealed record Policy(
    [property: JsonPropertyName("perSprintPercent")] double PerSprintPercent,
    [property: JsonPropertyName("cumulativePercent")] double CumulativePercent,
    [property: JsonPropertyName("allocPercent")] double AllocPercent,
    [property: JsonPropertyName("lohTripwireBytes")] double LohTripwireBytes,
    [property: JsonPropertyName("timeSignificanceK")] double TimeSignificanceK,
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
/// When <see langword="true"/>, a time or allocation percentage breach in
/// this category fails the gate. When <see langword="false"/>, both
/// percentage axes are reported but never fail. The LOH tripwire (D-333)
/// ignores this flag and can fail either way.
/// </param>
public sealed record PolicyCategory(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("namespacePrefix")] string NamespacePrefix,
    [property: JsonPropertyName("baseline")] string Baseline,
    [property: JsonPropertyName("gating")] bool Gating);

// --- evaluation model ---

/// <summary>
/// Classification of a single benchmark's time-axis comparison (D-333).
/// </summary>
public enum TimeClass {
    /// <summary>Within threshold on a CPU-matched comparison; no action needed.</summary>
    Ok,
    /// <summary>Non-gating category — reported for information, never fails the gate.</summary>
    Informational,
    /// <summary>
    /// Fresh run's CPU differs from the baseline's CPU on at least one of the
    /// rolling/origin sides, or either host is unrecorded — a genuine
    /// hardware-driven time swing is indistinguishable from a regression, so
    /// the comparison is reported but never fails the gate.
    /// </summary>
    CpuMismatch,
    /// <summary>Present in the fresh run but absent from the rolling baseline; treated as informational.</summary>
    NewBenchmark,
    /// <summary>The rolling baseline file for this category does not exist yet; establishing.</summary>
    NoBaseline,
    /// <summary>Fresh mean exceeds the rolling baseline by more than a noise-adjusted <see cref="Policy.PerSprintPercent"/> (D-333).</summary>
    PerSprintBreach,
    /// <summary>Fresh mean exceeds the origin baseline by more than <see cref="Policy.CumulativePercent"/>.</summary>
    CumulativeBreach,
}

/// <summary>
/// Classification of a single benchmark's allocation-axis comparison (D-333).
/// Allocation is deterministic and CPU-independent, so unlike <see cref="TimeClass"/>
/// it is never suppressed by a CPU mismatch.
/// </summary>
public enum AllocClass {
    /// <summary>Within <see cref="Policy.AllocPercent"/> of the rolling baseline; no action needed.</summary>
    Ok,
    /// <summary>Non-gating category — percentage creep reported for information, never fails the gate.</summary>
    Informational,
    /// <summary>Present in the fresh run but absent from the rolling baseline; treated as informational.</summary>
    NewBenchmark,
    /// <summary>The rolling baseline file for this category does not exist yet; establishing.</summary>
    NoBaseline,
    /// <summary>Fresh allocation exceeds the rolling baseline by more than <see cref="Policy.AllocPercent"/> on a gating category.</summary>
    PerSprintBreach,
    /// <summary>
    /// Fresh allocation meets or exceeds <see cref="Policy.LohTripwireBytes"/> — the absolute
    /// Large Object Heap tripwire. Fires regardless of the category's <see cref="PolicyCategory.Gating"/>
    /// flag; this is what would have caught the D-332 defect on day one.
    /// </summary>
    LohTripwireBreach,
}

/// <summary>
/// Comparison result for a single benchmark against both the time and
/// allocation axes (D-333). The two axes are classified independently: a
/// benchmark can read time-informational under a CPU mismatch while its
/// allocation axis still gates.
/// </summary>
/// <param name="Category">Name of the policy category this benchmark belongs to.</param>
/// <param name="FullName">Fully qualified benchmark method name.</param>
/// <param name="TimePerSprintPercent">Percentage change relative to the rolling baseline, or <see langword="null"/> when unavailable.</param>
/// <param name="TimeCumulativePercent">Percentage change relative to the frozen origin baseline, or <see langword="null"/> when unavailable.</param>
/// <param name="TimeClass">Classification of the time-axis result.</param>
/// <param name="AllocPercent">Percentage change in bytes allocated per operation relative to the rolling baseline, or <see langword="null"/> when unavailable.</param>
/// <param name="AllocBytes">Fresh bytes allocated per operation, or <see langword="null"/> when unavailable.</param>
/// <param name="AllocClass">Classification of the allocation-axis result.</param>
public sealed record BenchmarkDelta(
    string Category,
    string FullName,
    double? TimePerSprintPercent,
    double? TimeCumulativePercent,
    TimeClass TimeClass,
    double? AllocPercent,
    double? AllocBytes,
    AllocClass AllocClass);

/// <summary>
/// Overall outcome of a benchmark gate evaluation run.
/// </summary>
public enum Outcome {
    /// <summary>Every gating benchmark is within threshold on both axes.</summary>
    Pass,
    /// <summary>At least one gating benchmark exceeds a threshold on either axis, or the LOH tripwire fired.</summary>
    Regression,
}

/// <summary>
/// Full result of a gate evaluation: the outcome, per-benchmark deltas, and
/// informational notes.
/// </summary>
/// <param name="Outcome">Overall pass/regression verdict.</param>
/// <param name="Deltas">Per-benchmark comparison results.</param>
/// <param name="Notes">Informational messages (missing baselines, CPU mismatches, etc.).</param>
public sealed record EvaluationReport(
    Outcome Outcome,
    IReadOnlyList<BenchmarkDelta> Deltas,
    IReadOnlyList<string> Notes);

/// <summary>
/// A single benchmark's measured mean, standard deviation and allocation, as
/// read from either a fresh run or a committed baseline.
/// </summary>
/// <param name="Mean">Mean execution time in nanoseconds.</param>
/// <param name="StandardDeviation">Standard deviation of execution time in nanoseconds.</param>
/// <param name="AllocatedBytes">Bytes allocated per operation, or <see langword="null"/> if unavailable.</param>
public sealed record BenchmarkMeasurement(double Mean, double StandardDeviation, double? AllocatedBytes);

/// <summary>
/// A single side of a comparison: the per-benchmark measurements and the host they were measured on.
/// </summary>
/// <param name="Host">Machine and runtime metadata, or <see langword="null"/> if not available.</param>
/// <param name="Measurements">Map of fully qualified benchmark name to its measurement.</param>
public sealed record BaselineSide(
    BdnHostEnvironmentInfo? Host,
    IReadOnlyDictionary<string, BenchmarkMeasurement> Measurements);

/// <summary>
/// Core logic for the benchmark regression gate (D-313, hardened by D-333). All
/// methods are pure or thin file-IO wrappers so the gate logic is unit-testable
/// with in-memory inputs.
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

        foreach (var category in policy.Categories) {
            var freshInCategory = fresh.Measurements
                .Where(kv => kv.Key.StartsWith(category.NamespacePrefix, StringComparison.Ordinal))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .ToList();

            if (freshInCategory.Count == 0) {
                notes.Add($"{category.Name}: no fresh benchmarks matched '{category.NamespacePrefix}' — nothing to compare.");
                continue;
            }

            var rolling = loadBaseline(category.Baseline);
            if (rolling is null) {
                foreach (var (name, freshM) in freshInCategory)
                    deltas.Add(new BenchmarkDelta(category.Name, name, null, null, TimeClass.NoBaseline, null, freshM.AllocatedBytes, AllocClass.NoBaseline));
                notes.Add($"{category.Name}: rolling baseline '{category.Baseline}' not found — establishing, no comparison.");
                continue;
            }

            var origin = loadBaseline(OriginName(category.Baseline));
            if (origin is null)
                notes.Add($"{category.Name}: origin baseline '{OriginName(category.Baseline)}' not found — cumulative axis skipped.");

            var sameCpuRolling = SameCpu(fresh.Host, rolling.Host);
            var sameCpuOrigin = origin is not null && SameCpu(fresh.Host, origin.Host);
            if (!sameCpuRolling) {
                notes.Add(
                    $"{category.Name}: CPU mismatch — fresh '{CpuOf(fresh.Host)}' vs rolling baseline " +
                    $"'{CpuOf(rolling.Host)}'. Time comparison is informational; allocation still gates.");
            }

            foreach (var (name, freshM) in freshInCategory) {
                if (!rolling.Measurements.TryGetValue(name, out var rollingM)) {
                    // A fresh-only benchmark has no rolling counterpart to delta against,
                    // but the absolute LOH tripwire is unconditional (D-333): apply it here
                    // too so a newly added over-allocating benchmark fails on day one rather
                    // than being frozen into the next baseline.
                    var newAllocClass = BreachesLohTripwire(freshM, policy) ? AllocClass.LohTripwireBreach : AllocClass.NewBenchmark;
                    if (newAllocClass is AllocClass.LohTripwireBreach) regression = true;
                    deltas.Add(new BenchmarkDelta(category.Name, name, null, null, TimeClass.NewBenchmark, null, freshM.AllocatedBytes, newAllocClass));
                    continue;
                }

                var (timePerSprint, timeCumulative, timeClass) = ClassifyTime(
                    freshM, rollingM,
                    origin is not null && origin.Measurements.TryGetValue(name, out var originM) ? originM : null,
                    category.Gating, sameCpuRolling, sameCpuOrigin, policy);

                var (allocPercent, allocClass) = ClassifyAlloc(freshM, rollingM, category.Gating, policy);

                if (timeClass is TimeClass.PerSprintBreach or TimeClass.CumulativeBreach) regression = true;
                if (allocClass is AllocClass.PerSprintBreach or AllocClass.LohTripwireBreach) regression = true;

                deltas.Add(new BenchmarkDelta(category.Name, name, timePerSprint, timeCumulative, timeClass, allocPercent, freshM.AllocatedBytes, allocClass));
            }
        }

        return new EvaluationReport(regression ? Outcome.Regression : Outcome.Pass, deltas, notes);
    }

    private static (double? PerSprint, double? Cumulative, TimeClass Class) ClassifyTime(
        BenchmarkMeasurement fresh,
        BenchmarkMeasurement rolling,
        BenchmarkMeasurement? origin,
        bool gating,
        bool sameCpuRolling,
        bool sameCpuOrigin,
        Policy policy) {
        var perSprint = Percent(fresh.Mean, rolling.Mean);
        double? cumulative = origin is not null ? Percent(fresh.Mean, origin.Mean) : null;

        if (!gating)
            return (perSprint, cumulative, TimeClass.Informational);

        if (sameCpuRolling) {
            var relativeStdDev = Math.Max(RelativePercent(fresh), RelativePercent(rolling));
            var threshold = Math.Max(policy.PerSprintPercent, policy.TimeSignificanceK * relativeStdDev);
            if (perSprint > threshold)
                return (perSprint, cumulative, TimeClass.PerSprintBreach);
        }

        var canCheckCumulative = cumulative is not null && sameCpuOrigin;
        if (canCheckCumulative && cumulative!.Value > policy.CumulativePercent)
            return (perSprint, cumulative, TimeClass.CumulativeBreach);

        // "Blocked" means an axis that actually has baseline data to compare against
        // was withheld from gating by a CPU mismatch — not merely that the axis has
        // no data at all (a missing origin, say, is reported Ok if per-sprint is fine).
        var cpuBlocked = !sameCpuRolling || (cumulative is not null && !sameCpuOrigin);
        return (perSprint, cumulative, cpuBlocked ? TimeClass.CpuMismatch : TimeClass.Ok);
    }

    private static (double? Percent, AllocClass Class) ClassifyAlloc(
        BenchmarkMeasurement fresh,
        BenchmarkMeasurement rolling,
        bool gating,
        Policy policy) {
        if (BreachesLohTripwire(fresh, policy))
            return (null, AllocClass.LohTripwireBreach);

        if (fresh.AllocatedBytes is null || rolling.AllocatedBytes is null)
            return (null, AllocClass.Ok);

        var percent = Percent(fresh.AllocatedBytes.Value, rolling.AllocatedBytes.Value);
        if (!gating)
            return (percent, AllocClass.Informational);
        return percent > policy.AllocPercent ? (percent, AllocClass.PerSprintBreach) : (percent, AllocClass.Ok);
    }

    /// <summary>
    /// Whether a fresh measurement meets or exceeds the absolute LOH tripwire
    /// (<see cref="Policy.LohTripwireBytes"/>, D-333). The single source of truth for
    /// the unconditional allocation ceiling, applied both to benchmarks that have a
    /// rolling counterpart and to fresh-only ones with none.
    /// </summary>
    private static bool BreachesLohTripwire(BenchmarkMeasurement fresh, Policy policy)
        => fresh.AllocatedBytes is { } bytes && bytes >= policy.LohTripwireBytes;

    /// <summary>
    /// A benchmark's standard deviation as a percentage of its own mean —
    /// the noise-relative signal the significance-aware time gate (D-333)
    /// compares against <see cref="Policy.TimeSignificanceK"/>.
    /// </summary>
    private static double RelativePercent(BenchmarkMeasurement m)
        => Math.Abs(m.Mean) < 1e-3 ? 0 : m.StandardDeviation / m.Mean * 100.0;

    /// <summary>
    /// Computes the percentage change of <paramref name="fresh"/> relative to
    /// <paramref name="baseline"/>. A positive value means the fresh run is slower.
    /// Returns <c>0</c> when <paramref name="baseline"/> is zero to avoid division by zero.
    /// </summary>
    /// <param name="fresh">The new value.</param>
    /// <param name="baseline">The reference value.</param>
    /// <returns>Signed percentage change, e.g. <c>+5.0</c> for 5% higher.
    /// Returns <c>0</c> when <paramref name="baseline"/> is effectively zero
    /// (below 1 picosecond/byte) to avoid division by zero.</returns>
    public static double Percent(double fresh, double baseline)
        => Math.Abs(baseline) < 1e-3 ? 0 : (fresh - baseline) / baseline * 100.0;

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
    /// report the same CPU model (D-333). Hosted runners cannot be CPU-pinned — the
    /// same <c>windows-latest</c> label can serve different silicon run to run — so this
    /// keys on <see cref="BdnHostEnvironmentInfo.ProcessorName"/> rather than the runner
    /// label. Either side missing or empty (including a placeholder such as
    /// <c>"Unknown processor"</c>) is never treated as a match — an unrecorded CPU can't
    /// be verified equal to anything.
    /// </summary>
    /// <param name="a">First host, or <see langword="null"/> if unavailable.</param>
    /// <param name="b">Second host, or <see langword="null"/> if unavailable.</param>
    public static bool SameCpu(BdnHostEnvironmentInfo? a, BdnHostEnvironmentInfo? b)
        => a?.ProcessorName is { Length: > 0 } an && !IsUnknownProcessor(an)
           && b?.ProcessorName is { Length: > 0 } bn && !IsUnknownProcessor(bn)
           && string.Equals(an, bn, StringComparison.Ordinal);

    /// <summary>
    /// Whether a processor name is BenchmarkDotNet's <c>CpuInfo.Unknown</c> fallback
    /// (<c>"Unknown processor"</c>), emitted when hardware detection fails. Two hosts
    /// that both failed detection are not verified equal, so this placeholder never
    /// counts as a CPU match (D-333).
    /// </summary>
    private static bool IsUnknownProcessor(string name)
        => string.Equals(name, "Unknown processor", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The CPU model name for report/note rendering, or a fallback label when unrecorded.
    /// </summary>
    /// <param name="host">Host environment info, or <see langword="null"/>.</param>
    public static string CpuOf(BdnHostEnvironmentInfo? host)
        => host?.ProcessorName is { Length: > 0 } name ? name : "unknown CPU";

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
    /// the mean, standard deviation and allocation for each benchmark.
    /// </summary>
    /// <param name="report">The report to convert.</param>
    /// <returns>
    /// A <see cref="BaselineSide"/> whose <see cref="BaselineSide.Measurements"/> map
    /// contains every benchmark with a non-empty name and non-null statistics.
    /// </returns>
    public static BaselineSide ToSide(BdnReport report) {
        var measurements = new Dictionary<string, BenchmarkMeasurement>(StringComparer.Ordinal);
        foreach (var b in report.Benchmarks ?? []) {
            if (b.FullName is { Length: > 0 } name && b.Statistics is { } stats)
                measurements[name] = new BenchmarkMeasurement(stats.Mean, stats.StandardDeviation, b.Memory?.BytesAllocatedPerOperation);
        }
        return new BaselineSide(report.HostEnvironmentInfo, measurements);
    }

    /// <summary>
    /// Merges every <c>*-report-full.json</c> found (recursively) under
    /// <paramref name="resultsDir"/> into a single <see cref="BaselineSide"/>.
    /// The host is taken from the first file; later files' benchmark measurements
    /// overwrite earlier ones if names collide.
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

        var measurements = new Dictionary<string, BenchmarkMeasurement>(StringComparer.Ordinal);
        BdnHostEnvironmentInfo? host = null;
        foreach (var file in files) {
            var report = LoadReport(file);
            host ??= report.HostEnvironmentInfo;
            foreach (var (k, v) in ToSide(report).Measurements)
                measurements[k] = v;
        }
        return new BaselineSide(host, measurements);
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
