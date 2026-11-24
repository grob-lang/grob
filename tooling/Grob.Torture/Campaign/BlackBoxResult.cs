namespace Grob.Torture.Campaign;

/// <summary>One reproducer's black-box check, under one signature it was minimised against.</summary>
/// <param name="Signature">The signature.</param>
/// <param name="Reproducer">The reproducer.</param>
/// <param name="Verdict">What the CLI showed.</param>
/// <param name="ExitCode">The CLI's exit code.</param>
internal sealed record BlackBoxResult(string Signature, string Reproducer, BlackBoxVerdict Verdict, int ExitCode);
