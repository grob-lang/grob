using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Grob.Torture.Campaign;

/// <summary>
/// Runs <c>grob run &lt;script&gt;</c> through the real CLI for the black-box check (gate report
/// §4.3), with stdin closed and a timeout, capturing stderr and the exit code.
/// </summary>
/// <param name="grobPath">The CLI's executable.</param>
[ExcludeFromCodeCoverage(Justification = "Process binding only: it starts the real CLI and returns its stderr and exit code. Classifying that output is BlackBox.Classify, which the tests cover, and the supervisor uses this runner through IGrobRunner, which they fake.")]
internal sealed class ProcessGrobRunner(string grobPath) : IGrobRunner {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    public GrobOutput Run(string scriptPath) {
        var start = new ProcessStartInfo(grobPath) {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add(scriptPath);

        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"{grobPath} did not start");
        process.StandardInput.Close();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        if (!process.WaitForExit(Timeout)) {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return new GrobOutput(-1, stderr.Result, TimedOut: true);
        }
        _ = stdout.Result;
        return new GrobOutput(process.ExitCode, stderr.Result, TimedOut: false);
    }
}
