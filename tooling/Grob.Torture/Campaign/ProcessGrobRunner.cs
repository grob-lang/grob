using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

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

        // stdout is drained in fixed-size chunks and dropped, so a reproducer that prints without
        // end, with or without a newline, neither blocks on a full pipe nor fills the supervisor's
        // memory; a line reader would hold an unfinished line whole. stderr is what the check reads.
        var stderr = new StringBuilder();
        using Process process = Process.Start(start) ?? throw new InvalidOperationException($"{grobPath} did not start");
        Task stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        process.ErrorDataReceived += (_, e) => {
            if (e.Data is not null) lock (stderr) stderr.Append(e.Data).Append('\n');
        };
        process.BeginErrorReadLine();
        process.StandardInput.Close();

        bool ended = process.WaitForExit(Timeout);
        if (!ended) process.Kill(entireProcessTree: true);
        process.WaitForExit(); // waits for stderr to drain
        stdout.GetAwaiter().GetResult();
        lock (stderr) return new GrobOutput(ended ? process.ExitCode : -1, stderr.ToString(), TimedOut: !ended);
    }
}
