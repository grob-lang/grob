using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Grob.Torture.Campaign;

/// <summary>
/// Starts each campaign worker as a child process of this executable, running
/// <c>campaign-worker</c>, so that a stack overflow (R-70) kills the worker and not the campaign
/// (gate report §4.4). The worker's stderr is read and dropped: a death's own trace says nothing the
/// journal does not.
/// </summary>
/// <param name="root">The repository root the workers load the seed corpus from.</param>
[ExcludeFromCodeCoverage(Justification = "Process binding only: it starts a child process and returns its exit code. The supervisor's handling of every exit is tested through IWorkerLauncher with in-process and dying fakes, and the worker command through Cli.Run.")]
internal sealed class ProcessWorkerLauncher(string root) : IWorkerLauncher {
    /// <inheritdoc/>
    public int Run(string directory, int shard) {
        var start = new ProcessStartInfo(Executable()) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (string argument in Prefix()) start.ArgumentList.Add(argument);
        foreach (string argument in new[] { "campaign-worker", "--out", directory, "--shard", shard.ToString(CultureInfo.InvariantCulture), "--root", root }) {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("the worker did not start");
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        return process.ExitCode;
    }

    // Under `dotnet Grob.Torture.dll` the process is dotnet itself, so the assembly is passed to it.
    private static string Executable() => Environment.ProcessPath ?? throw new InvalidOperationException("no process path");

    private static string[] Prefix() =>
        Path.GetFileNameWithoutExtension(Executable()).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            ? [typeof(ProcessWorkerLauncher).Assembly.Location]
            : [];
}
