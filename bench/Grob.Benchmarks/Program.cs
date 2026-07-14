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

// The frozen script copies the stability loop reads from (§7.3's decoupling rationale).
static string StabilityFixturesDir() => Path.Join(AppContext.BaseDirectory, "Fixtures", "Stability");

// The six Sprint-8-runnable scripts and each one's expected exit code, in fixed order.
static IReadOnlyList<StabilityScript> StabilityScripts() {
    string dir = StabilityFixturesDir();
    // Fixed, deliberate order — not directory-enumeration order — so calibration and
    // stability runs are reproducible run to run. errors.grob is the one script that
    // exits 42 by design (D-337); every other script exits 0.
    (string Name, int ExpectedExitCode)[] scripts = [
        ("hello.grob", 0),
        ("calculator.grob", 0),
        ("functions.grob", 0),
        ("types.grob", 0),
        ("errors.grob", 42),
        ("stdlib.grob", 0),
    ];
    return [.. scripts.Select(s => new StabilityScript(Path.Join(dir, s.Name), s.ExpectedExitCode))];
}

// Runs the --calibrate characterisation pass and prints its raw numbers.
static void RunCalibration() {
    IReadOnlyList<StabilityScript> scripts = StabilityScripts();
    CalibrationResult result = StabilityRunner.Calibrate(scripts);

    Console.WriteLine($"Wall-clock, first iteration (JIT warm-up included, {scripts.Count} scripts): {result.MillisecondsPerIteration:F3} ms");
    Console.WriteLine($"Wall-clock, steady-state per iteration (incl. forceFullCollection each sample): {result.SteadyStateMillisecondsPerIteration:F3} ms");
    Console.WriteLine($"Heap after 10 iterations: {result.HeapAfterTenIterationsBytes} bytes");
    Console.WriteLine("Steady-state heap samples (post-warmup):");
    foreach (long sample in result.SteadyStateHeapSamplesBytes) {
        Console.WriteLine($"  {sample} bytes");
    }
}

// Runs the --calibrate-long checkpoint sweep and prints the heap-by-iteration table.
static void RunLongCalibration() {
    IReadOnlyList<StabilityScript> scripts = StabilityScripts();
    int[] checkpoints = [10, 50, 100, 250, 500, 1000, 2000, 5000, 10000];
    IReadOnlyList<HeapCheckpoint> results = StabilityRunner.CalibrateCheckpoints(scripts, checkpoints);

    Console.WriteLine("Iteration  HeapBytes");
    foreach (HeapCheckpoint checkpoint in results) {
        Console.WriteLine($"{checkpoint.Iteration,9}  {checkpoint.HeapBytes}");
    }
}

// Runs the --stability locked test against the committed stability.json and returns
// the process exit code (0 pass, 1 fail).
static int RunStability() {
    string baselinePath = Path.Join(AppContext.BaseDirectory, "..", "..", "..", "baseline", "stability.json");
    baselinePath = Path.GetFullPath(baselinePath);
    StabilityConfig config = StabilityConfig.Read(baselinePath);
    IReadOnlyList<StabilityScript> scripts = StabilityScripts();

    StabilityResult result = StabilityRunner.RunStability(scripts, config);

    Console.WriteLine($"Post-warmup heap: {result.PostWarmupHeapBytes} bytes");
    Console.WriteLine($"Final heap:       {result.FinalHeapBytes} bytes");
    Console.WriteLine($"Within own tolerance ({config.TolerancePercent}%):    {result.WithinOwnTolerance}");
    Console.WriteLine($"Within last-passing baseline: {result.WithinLastPassingBaseline}");
    Console.WriteLine(result.Passed ? "PASS" : "FAIL");

    return result.Passed ? 0 : 1;
}
