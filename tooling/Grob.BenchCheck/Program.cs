using Grob.BenchCheck;

// Grob.BenchCheck — the benchmark regression gate (D-313).
//
// Reads the committed baselines and a fresh BenchmarkDotNet run, computes the
// two-axis comparison (per-sprint vs rolling, cumulative vs frozen origin),
// writes a delta table to stdout and to the GitHub job summary, and sets the
// exit code so the workflow goes red on a breach.
//
//   exit 0  pass
//   exit 1  regression breach (per-sprint or cumulative, on a gating category)
//   exit 2  cannot compare / usage / IO error

return Cli.Run(args);
