using BenchmarkDotNet.Running;

using Grob.Benchmarks.Stability;

if (args.Length > 0 && args[0] == "--calibrate") {
    RunCalibration();
    return;
}

if (args.Length > 0 && args[0] == "--calibrate-long") {
    RunLongCalibration();
    return;
}

if (args.Length > 0 && args[0] == "--stability") {
    Environment.Exit(RunStability());
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return;

static string StabilityFixturesDir() => Path.Join(AppContext.BaseDirectory, "Fixtures", "Stability");

static IReadOnlyList<string> StabilityScriptPaths() {
    string dir = StabilityFixturesDir();
    // Fixed, deliberate order — not directory-enumeration order — so calibration and
    // stability runs are reproducible run to run.
    string[] names = ["hello.grob", "calculator.grob", "functions.grob", "types.grob", "errors.grob", "stdlib.grob"];
    return [.. names.Select(name => Path.Join(dir, name))];
}

static void RunCalibration() {
    IReadOnlyList<string> scripts = StabilityScriptPaths();
    CalibrationResult result = StabilityRunner.Calibrate(scripts);

    Console.WriteLine($"Wall-clock, first iteration (JIT warm-up included, {scripts.Count} scripts): {result.MillisecondsPerIteration:F3} ms");
    Console.WriteLine($"Wall-clock, steady-state per iteration (incl. forceFullCollection each sample): {result.SteadyStateMillisecondsPerIteration:F3} ms");
    Console.WriteLine($"Heap after 10 iterations: {result.HeapAfterTenIterationsBytes} bytes");
    Console.WriteLine("Steady-state heap samples (post-warmup):");
    foreach (long sample in result.SteadyStateHeapSamplesBytes) {
        Console.WriteLine($"  {sample} bytes");
    }
}

static void RunLongCalibration() {
    IReadOnlyList<string> scripts = StabilityScriptPaths();
    int[] checkpoints = [10, 50, 100, 250, 500, 1000, 2000, 5000, 10000];
    IReadOnlyList<HeapCheckpoint> results = StabilityRunner.CalibrateCheckpoints(scripts, checkpoints);

    Console.WriteLine("Iteration  HeapBytes");
    foreach (HeapCheckpoint checkpoint in results) {
        Console.WriteLine($"{checkpoint.Iteration,9}  {checkpoint.HeapBytes}");
    }
}

static int RunStability() {
    string baselinePath = Path.Join(AppContext.BaseDirectory, "..", "..", "..", "baseline", "stability.json");
    baselinePath = Path.GetFullPath(baselinePath);
    StabilityConfig config = StabilityConfig.Read(baselinePath);
    IReadOnlyList<string> scripts = StabilityScriptPaths();

    StabilityResult result = StabilityRunner.RunStability(scripts, config);

    Console.WriteLine($"Post-warmup heap: {result.PostWarmupHeapBytes} bytes");
    Console.WriteLine($"Final heap:       {result.FinalHeapBytes} bytes");
    Console.WriteLine($"Within own tolerance ({config.TolerancePercent}%):    {result.WithinOwnTolerance}");
    Console.WriteLine($"Within last-passing baseline: {result.WithinLastPassingBaseline}");
    Console.WriteLine(result.Passed ? "PASS" : "FAIL");

    return result.Passed ? 0 : 1;
}
