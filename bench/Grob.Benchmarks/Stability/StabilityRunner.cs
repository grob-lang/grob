using System.Diagnostics;

using Grob.Cli;

namespace Grob.Benchmarks.Stability;

/// <summary>Result of one calibration pass (Sprint 8 Increment F, D-302/D-346).</summary>
public sealed record CalibrationResult(
    double MillisecondsPerIteration,
    long HeapAfterTenIterationsBytes,
    IReadOnlyList<long> SteadyStateHeapSamplesBytes,
    double SteadyStateMillisecondsPerIteration);

/// <summary>One (iteration index, forced-GC heap bytes) checkpoint from a long calibration pass.</summary>
public sealed record HeapCheckpoint(int Iteration, long HeapBytes);

/// <summary>Result of a locked stability run against a <see cref="StabilityConfig"/>.</summary>
public sealed record StabilityResult(
    long PostWarmupHeapBytes,
    long FinalHeapBytes,
    bool WithinOwnTolerance,
    bool WithinLastPassingBaseline,
    bool Passed);

/// <summary>
/// The long-run stability test (<c>grob-benchmarking-strategy.md</c> §6/§7.5). A
/// separate console loop, deliberately not BenchmarkDotNet — it measures steady-state
/// managed heap across many large operations rather than the timing of one small
/// operation run many times. Each iteration runs every script in
/// <c>Fixtures/Stability/</c> through <see cref="RunCommand"/>, which constructs a
/// fresh <c>VirtualMachine</c> and re-registers every stdlib plugin per call — the
/// "fresh VM per iteration" requirement is satisfied by reuse, not by this class
/// re-implementing VM construction.
/// </summary>
public static class StabilityRunner {
    /// <summary>
    /// Runs one iteration of every script in <paramref name="scriptPaths"/>, discarding
    /// their stdout/stderr (the stability test cares about heap behaviour, not output).
    /// </summary>
    private static void RunOneIteration(IReadOnlyList<string> scriptPaths) {
        foreach (string path in scriptPaths) {
            _ = new RunCommand(TextWriter.Null, TextWriter.Null).Run(path);
        }
    }

    /// <summary>
    /// The calibration ritual (§6, §11): a single-iteration characterisation pass.
    /// Times one full iteration of every script, then runs a further
    /// <paramref name="sampleCount"/> iterations recording
    /// <c>GC.GetTotalMemory(forceFullCollection: true)</c> after each, past a ten-
    /// iteration warmup, so the caller can eyeball wall-clock time, steady-state heap
    /// and iteration-to-iteration variance before locking numbers into
    /// <c>stability.json</c>. Prints nothing and asserts nothing — a characterisation
    /// tool, not a test.
    /// </summary>
    public static CalibrationResult Calibrate(IReadOnlyList<string> scriptPaths, int sampleCount = 10) {
        var stopwatch = Stopwatch.StartNew();
        RunOneIteration(scriptPaths);
        stopwatch.Stop();
        double msPerIteration = stopwatch.Elapsed.TotalMilliseconds;

        for (int i = 0; i < 9; i++) RunOneIteration(scriptPaths);
        long heapAfterTen = GC.GetTotalMemory(forceFullCollection: true);

        var samples = new List<long>(sampleCount);
        var sampleStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < sampleCount; i++) {
            RunOneIteration(scriptPaths);
            samples.Add(GC.GetTotalMemory(forceFullCollection: true));
        }
        sampleStopwatch.Stop();
        double steadyStateMsPerIteration = sampleStopwatch.Elapsed.TotalMilliseconds / sampleCount;

        return new CalibrationResult(msPerIteration, heapAfterTen, samples, steadyStateMsPerIteration);
    }

    /// <summary>
    /// Extends the calibration ritual across a longer horizon than
    /// <see cref="Calibrate"/>'s tightly-clustered ten samples: records forced-GC heap
    /// at each iteration index in <paramref name="checkpoints"/> (ascending) over one
    /// continuous run, so the caller can see whether early heap growth plateaus (a
    /// one-time cache/registry warm-up cost) or keeps climbing (a real per-iteration
    /// retention leak) before locking a tolerance.
    /// </summary>
    public static IReadOnlyList<HeapCheckpoint> CalibrateCheckpoints(
            IReadOnlyList<string> scriptPaths, IReadOnlyList<int> checkpoints) {
        var results = new List<HeapCheckpoint>(checkpoints.Count);
        int lastCheckpoint = 0;
        foreach (int checkpoint in checkpoints) {
            for (int i = lastCheckpoint; i < checkpoint; i++) RunOneIteration(scriptPaths);
            results.Add(new HeapCheckpoint(checkpoint, GC.GetTotalMemory(forceFullCollection: true)));
            lastCheckpoint = checkpoint;
        }
        return results;
    }

    /// <summary>
    /// Runs the locked stability test: <paramref name="config"/>.Iterations iterations
    /// of every script, recording heap at the <paramref name="config"/>.Warmup boundary
    /// and at the final iteration (<c>forceFullCollection: true</c> at both points, per
    /// §7.5). Passes when the final heap is within <paramref name="config"/>.
    /// TolerancePercent of the post-warmup heap, and — if a prior passing run exists
    /// (<c>LastPassingHeapBytes &gt; 0</c>) — within tolerance of that historical value
    /// too, so slow cross-release growth shows up even when each individual run is
    /// within its own tolerance (§7.6).
    /// </summary>
    public static StabilityResult RunStability(IReadOnlyList<string> scriptPaths, StabilityConfig config) {
        long postWarmupHeap = 0;
        for (int iteration = 1; iteration <= config.Iterations; iteration++) {
            RunOneIteration(scriptPaths);
            if (iteration == config.Warmup) {
                postWarmupHeap = GC.GetTotalMemory(forceFullCollection: true);
            }
        }

        long finalHeap = GC.GetTotalMemory(forceFullCollection: true);

        bool withinOwnTolerance = WithinTolerance(finalHeap, postWarmupHeap, config.TolerancePercent);
        bool withinBaseline = config.LastPassingHeapBytes <= 0
            || WithinTolerance(finalHeap, config.LastPassingHeapBytes, config.TolerancePercent);

        return new StabilityResult(
            postWarmupHeap,
            finalHeap,
            withinOwnTolerance,
            withinBaseline,
            Passed: withinOwnTolerance && withinBaseline);
    }

    private static bool WithinTolerance(long value, long reference, double tolerancePercent) {
        if (reference <= 0) return true;
        double deltaPercent = Math.Abs(value - reference) / (double)reference * 100.0;
        return deltaPercent <= tolerancePercent;
    }
}
