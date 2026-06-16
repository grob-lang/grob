namespace Grob.BenchCheck;

internal sealed record Options(string ResultsDir, string BaselineDir, string PolicyPath, string? SummaryPath) {
    public static Options Parse(string[] args) {
        var results = "BenchmarkDotNet.Artifacts/results";
        var baseline = "bench/Grob.Benchmarks/baseline";
        string? policy = null;
        string? summary = Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY");

        for (var i = 0; i < args.Length; i++) {
            switch (args[i]) {
                case "--results": results = Next(args, ref i); break;
                case "--baseline": baseline = Next(args, ref i); break;
                case "--policy": policy = Next(args, ref i); break;
                case "--summary": summary = Next(args, ref i); break;
                case "-h" or "--help": throw new InvalidDataException(Usage);
                default: throw new InvalidDataException($"Unknown argument '{args[i]}'.{Environment.NewLine}{Usage}");
            }
        }

        policy ??= Path.Join(baseline, "policy.json");
        return new Options(results, baseline, policy, summary);
    }

    private static string Next(string[] args, ref int i)
        => ++i < args.Length ? args[i] : throw new InvalidDataException($"Missing value for '{args[i - 1]}'.");

    public const string Usage =
        "Usage: Grob.BenchCheck [--results <dir>] [--baseline <dir>] [--policy <file>] [--summary <file>]";
}
