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
/// allocation threshold and the list of benchmark categories. The two time
/// figures are retained and rendered for a reader's context only — time never
/// gates on any category (D-395/D-396), so neither drives a classification.
/// </summary>
/// <param name="PerSprintPercent">
/// Historical per-sprint time threshold, displayed in the CLI report header
/// for context. No longer enforced — time is informational everywhere
/// (D-395/D-396).
/// </param>
/// <param name="CumulativePercent">
/// Historical cumulative time threshold, displayed in the CLI report header
/// for context. No longer enforced — time is informational everywhere
/// (D-395/D-396).
/// </param>
/// <param name="AllocPercent">
/// Maximum allowed percentage increase in bytes allocated per operation
/// relative to the rolling baseline before an allocation-gating category is
/// declared a breach (D-333).
/// </param>
/// <param name="TimeSignificanceK">
/// Historical significance multiplier, displayed in the CLI report header for
/// context. No longer consulted — the significance-aware time gate it fed
/// (D-333) cannot see between-run variance and was retired in favour of time
/// being informational everywhere (D-395/D-396).
/// </param>
/// <param name="Categories">The benchmark categories to evaluate.</param>
public sealed record Policy(
    [property: JsonPropertyName("perSprintPercent")] double PerSprintPercent,
    [property: JsonPropertyName("cumulativePercent")] double CumulativePercent,
    [property: JsonPropertyName("allocPercent")] double AllocPercent,
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
/// <param name="AllocGating">
/// When <see langword="true"/>, an allocation-percentage breach in this
/// category fails the gate. When <see langword="false"/>, the percentage is
/// reported but never fails. Governs the allocation-percent axis only — the
/// time axes are informational for every category regardless of this flag
/// (D-395/D-396), and the absolute allocation ceiling (D-333,
/// category/fixture-shaped per D-391) ignores this flag too and can fail
/// either way.
/// </param>
/// <param name="AllocationCeilingBytes">
/// Absolute bytes-allocated-per-operation ceiling for every benchmark in this
/// category that has no entry of its own in
/// <see cref="BenchmarkAllocationCeilings"/>. <see langword="null"/> when the
/// category has no ceiling configured yet (e.g. <c>endToEnd</c> while F8 is
/// open) — an unconfigured ceiling never breaches (D-391).
/// </param>
/// <param name="BenchmarkAllocationCeilings">
/// Per-benchmark ceiling overrides, keyed by <see cref="BdnBenchmark.FullName"/>,
/// taking precedence over <see cref="AllocationCeilingBytes"/> for the named
/// benchmark. A single category-wide ceiling cannot serve fixtures whose
/// legitimate allocation differs by two or more orders of magnitude (D-385
/// Q2's "per-category, or per-fixture-shape" clause; D-391 derives these for
/// `vm`'s and `attribution`'s widest-spread fixtures).
/// </param>
public sealed record PolicyCategory(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("namespacePrefix")] string NamespacePrefix,
    [property: JsonPropertyName("baseline")] string Baseline,
    [property: JsonPropertyName("allocGating")] bool AllocGating,
    [property: JsonPropertyName("allocationCeilingBytes")] double? AllocationCeilingBytes = null,
    [property: JsonPropertyName("benchmarkAllocationCeilings")] IReadOnlyDictionary<string, double>? BenchmarkAllocationCeilings = null);

// --- evaluation model ---

/// <summary>
/// Classification of a single benchmark's time-axis comparison. Time never
/// gates the build on any category (D-395/D-396) — a within-run ×3σ noise
/// filter cannot see between-run variance, so every comparison that has data
/// to report reads <see cref="Informational"/>, unconditionally.
/// </summary>
public enum TimeClass {
    /// <summary>Reported for information; never fails the gate.</summary>
    Informational,
    /// <summary>Present in the fresh run but absent from the rolling baseline; treated as informational.</summary>
    NewBenchmark,
    /// <summary>The rolling baseline file for this category does not exist yet; establishing.</summary>
    NoBaseline,
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
    /// Fresh allocation meets or exceeds the category's absolute allocation ceiling —
    /// <see cref="PolicyCategory.BenchmarkAllocationCeilings"/> if the benchmark has an
    /// entry there, else <see cref="PolicyCategory.AllocationCeilingBytes"/>. Fires
    /// regardless of the category's <see cref="PolicyCategory.AllocGating"/> flag; this is
    /// what would have caught the D-332 defect on day one (D-333, category/fixture-shaped
    /// per D-391).
    /// </summary>
    CeilingBreach,
}

/// <summary>
/// Comparison result for a single benchmark against both the time and
/// allocation axes. The two axes are classified independently: time never
/// gates on any category (D-395/D-396), while the allocation axis can still
/// gate — a benchmark can read time-informational while its allocation axis
/// breaches.
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
    /// <summary>At least one gating benchmark exceeds a threshold on either axis, or an allocation ceiling fired.</summary>
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
    /// <see cref="JsonSerializerOptions"/> used when reading BenchmarkDotNet reports.
    /// Case-insensitive, allows comments and trailing commas, and ignores the many
    /// report members this tool does not map. Policy files use the stricter
    /// <see cref="PolicyJson"/> instead.
    /// </summary>
    public static readonly JsonSerializerOptions Json = new() {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// <see cref="JsonSerializerOptions"/> for <c>policy.json</c> only. Identical to
    /// <see cref="Json"/> except that an unmapped member is rejected rather than
    /// ignored: the policy file configures the build gate, so a field this tool does
    /// not understand — a <c>gating</c> left behind by D-396's rename to
    /// <c>allocGating</c>, or a plain typo — must fail loudly. Ignoring it would
    /// default <see cref="PolicyCategory.AllocGating"/> to <see langword="false"/> and
    /// stand the allocation-percent check down silently, which is the fail-open shape
    /// D-395 diagnosed. BenchmarkDotNet reports keep <see cref="Json"/>: their
    /// documents carry many members this tool deliberately does not map.
    /// </summary>
    private static readonly JsonSerializerOptions PolicyJson = new() {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
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

            if (!SameCpu(fresh.Host, rolling.Host)) {
                notes.Add(
                    $"{category.Name}: CPU mismatch — fresh '{CpuOf(fresh.Host)}' vs rolling baseline " +
                    $"'{CpuOf(rolling.Host)}'. Δ time per-sprint may reflect the CPU swing rather than a real " +
                    "change; allocation is CPU-independent and still gates when configured.");
            }

            // The cumulative axis is measured against the *origin*, whose CPU can
            // differ from the fresh run's even when the rolling baseline's does not —
            // the live case
            // for `compile.origin.json` and its "Unknown processor" capture. Without
            // this the report shows a cross-CPU cumulative percentage with nothing
            // explaining it.
            if (origin is not null && !SameCpu(fresh.Host, origin.Host)) {
                notes.Add(
                    $"{category.Name}: CPU mismatch — fresh '{CpuOf(fresh.Host)}' vs origin baseline " +
                    $"'{CpuOf(origin.Host)}'. Δ time cumulative may reflect the CPU swing rather than a real " +
                    "change; the allocation axes do not use the origin baseline and are unaffected.");
            }

            foreach (var (name, freshM) in freshInCategory) {
                if (!rolling.Measurements.TryGetValue(name, out var rollingM)) {
                    // A fresh-only benchmark has no rolling counterpart to delta against,
                    // but the absolute allocation ceiling is unconditional (D-333): apply
                    // it here too so a newly added over-allocating benchmark fails on day
                    // one rather than being frozen into the next baseline.
                    var newAllocClass = BreachesAllocationCeiling(freshM, name, category) ? AllocClass.CeilingBreach : AllocClass.NewBenchmark;
                    if (newAllocClass is AllocClass.CeilingBreach) regression = true;
                    deltas.Add(new BenchmarkDelta(category.Name, name, null, null, TimeClass.NewBenchmark, null, freshM.AllocatedBytes, newAllocClass));
                    continue;
                }

                var originM = origin is not null && origin.Measurements.TryGetValue(name, out var om) ? om : null;
                var (timePerSprint, timeCumulative) = ClassifyTime(freshM, rollingM, originM);

                var (allocPercent, allocClass) = ClassifyAlloc(freshM, rollingM, name, category, policy);

                if (allocClass is AllocClass.PerSprintBreach or AllocClass.CeilingBreach) regression = true;

                deltas.Add(new BenchmarkDelta(category.Name, name, timePerSprint, timeCumulative, TimeClass.Informational, allocPercent, freshM.AllocatedBytes, allocClass));
            }
        }

        return new EvaluationReport(regression ? Outcome.Regression : Outcome.Pass, deltas, notes);
    }

    /// <summary>
    /// Computes the per-sprint and cumulative time deltas for reporting. Time never
    /// gates on any category (D-395/D-396) — there is no threshold, CPU-identity or
    /// significance check here, only the percentage a reader sees in the report; the
    /// caller always classifies the result <see cref="TimeClass.Informational"/>.
    /// </summary>
    private static (double? PerSprint, double? Cumulative) ClassifyTime(
        BenchmarkMeasurement fresh,
        BenchmarkMeasurement rolling,
        BenchmarkMeasurement? origin) {
        var perSprint = Percent(fresh.Mean, rolling.Mean);
        double? cumulative = origin is not null ? Percent(fresh.Mean, origin.Mean) : null;
        return (perSprint, cumulative);
    }

    private static (double? Percent, AllocClass Class) ClassifyAlloc(
        BenchmarkMeasurement fresh,
        BenchmarkMeasurement rolling,
        string fullName,
        PolicyCategory category,
        Policy policy) {
        // Computed before classifying, so a ceiling breach still carries the rolling
        // delta: the row that failed the gate is the one whose Δ alloc a reader most
        // wants. Null only when a side reported no bytes and there is nothing to
        // delta against.
        var percent = fresh.AllocatedBytes is { } freshBytes && rolling.AllocatedBytes is { } rollingBytes
            ? Percent(freshBytes, rollingBytes)
            : (double?)null;

        if (BreachesAllocationCeiling(fresh, fullName, category))
            return (percent, AllocClass.CeilingBreach);

        if (percent is null)
            return (null, AllocClass.Ok);

        if (!category.AllocGating)
            return (percent, AllocClass.Informational);
        return percent > policy.AllocPercent ? (percent, AllocClass.PerSprintBreach) : (percent, AllocClass.Ok);
    }

    /// <summary>
    /// Whether a fresh measurement meets or exceeds the applicable absolute allocation
    /// ceiling (D-333, category/fixture-shaped per D-391) — the benchmark's own entry in
    /// <see cref="PolicyCategory.BenchmarkAllocationCeilings"/> if present, else the
    /// category's <see cref="PolicyCategory.AllocationCeilingBytes"/> default. A category
    /// with neither configured (e.g. <c>endToEnd</c> while F8 is open) never breaches —
    /// there is nothing to compare against yet. The single source of truth for the
    /// unconditional allocation ceiling, applied both to benchmarks that have a rolling
    /// counterpart and to fresh-only ones with none.
    /// </summary>
    private static bool BreachesAllocationCeiling(BenchmarkMeasurement fresh, string fullName, PolicyCategory category) {
        if (fresh.AllocatedBytes is not { } bytes)
            return false;
        if (category.BenchmarkAllocationCeilings?.TryGetValue(fullName, out var overrideCeiling) == true)
            return bytes >= overrideCeiling;
        return category.AllocationCeilingBytes is { } ceiling && bytes >= ceiling;
    }

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
    public static Policy LoadPolicy(string path) {
        try {
            return ParsePolicy(File.ReadAllText(path));
        } catch (JsonException ex) {
            throw new InvalidDataException($"Could not parse policy file '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Parses policy JSON text. Pure — the file-reading half is <see cref="LoadPolicy"/>,
    /// which is also where a failure gains the offending file's name.
    /// </summary>
    /// <param name="json">The policy JSON document.</param>
    /// <returns>The deserialised <see cref="Policy"/>.</returns>
    /// <exception cref="JsonException">
    /// The text is not valid policy JSON, or it carries a field this tool does not
    /// map (see <see cref="PolicyJson"/>).
    /// </exception>
    public static Policy ParsePolicy(string json)
        => JsonSerializer.Deserialize<Policy>(json, PolicyJson)
           ?? throw new JsonException("Policy JSON deserialised to null.");

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
