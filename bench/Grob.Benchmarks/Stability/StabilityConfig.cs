using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grob.Benchmarks.Stability;

/// <summary>
/// The hand-curated shape of <c>bench/Grob.Benchmarks/baseline/stability.json</c>
/// (Sprint 8 Increment F, D-302/D-346). Not BenchmarkDotNet output — read and written
/// directly as JSON, matching <c>grob-benchmarking-strategy.md</c> §7.6.
/// </summary>
public sealed class StabilityConfig {
    /// <summary>The calibration date (<c>yyyy-MM-dd</c>) the locked trio was derived on.</summary>
    [JsonPropertyName("calibrated")]
    public string Calibrated { get; set; } = "";

    /// <summary>The locked iteration count for the stability loop.</summary>
    [JsonPropertyName("iterations")]
    public int Iterations { get; set; }

    /// <summary>The locked warmup window (iteration index at which post-warmup heap is recorded).</summary>
    [JsonPropertyName("warmup")]
    public int Warmup { get; set; }

    /// <summary>The locked tolerance, as a percentage of the reference heap value.</summary>
    [JsonPropertyName("tolerancePercent")]
    public double TolerancePercent { get; set; }

    /// <summary>
    /// The steady-state heap size the last passing stability run observed, in bytes.
    /// <c>0</c> before any run has passed — the first run's own tolerance check is
    /// then the only gate (§7.6).
    /// </summary>
    [JsonPropertyName("lastPassingHeapBytes")]
    public long LastPassingHeapBytes { get; set; }

    /// <summary>The date (<c>yyyy-MM-dd</c>) of the last passing stability run.</summary>
    [JsonPropertyName("lastRun")]
    public string LastRun { get; set; } = "";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>Reads a <see cref="StabilityConfig"/> from the JSON file at <paramref name="path"/>.</summary>
    public static StabilityConfig Read(string path) {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<StabilityConfig>(json)
            ?? throw new InvalidOperationException($"stability.json at '{path}' deserialised to null.");
    }

    /// <summary>Writes this <see cref="StabilityConfig"/> as indented JSON to <paramref name="path"/>.</summary>
    public void Write(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
}
