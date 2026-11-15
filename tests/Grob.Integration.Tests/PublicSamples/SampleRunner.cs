using System.Text;
using System.Text.RegularExpressions;

using Grob.Cli;
using Grob.Integration.Tests.ErrorExamples;

namespace Grob.Integration.Tests.PublicSamples;

/// <summary>One diagnostic the CLI wrote: its code and the first quoted name in its message.</summary>
internal sealed record SampleDiagnostic(string Code, string? Subject);

/// <summary>What one run of a block wrote and returned.</summary>
/// <param name="ExitCode">The CLI's exit code.</param>
/// <param name="Diagnostics">Every diagnostic header on stderr, in order.</param>
/// <param name="Problems">Every stderr line <see cref="CliDiagnosticReader"/> could not account for, an internal error included.</param>
internal sealed record SampleRun(int ExitCode, IReadOnlyList<SampleDiagnostic> Diagnostics, IReadOnlyList<string> Problems);

/// <summary>
/// Runs a block through <see cref="RunCommand"/>'s own pipeline over the seam (D-428 Decision 3),
/// so the harness and the CLI cannot disagree. A block is written to a temporary file, because
/// the pipeline reads a path. The capabilities are the harness's fixed ones, the environment the
/// corpus fixture and stdin closed, so no block reads the host or waits for input.
/// </summary>
internal static partial class SampleRunner {
    private static readonly DateTime FixedInstant = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [GeneratedRegex(@"^(?:error|warning)\[(?<code>E\d{4})\]: (?<message>.*)$")]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"'(?<name>[^']+)'")]
    private static partial Regex SubjectPattern();

    public static SampleRun Run(string source, IReadOnlyDictionary<string, string> environment) {
        string path = Path.Join(Path.GetTempPath(), $"grob-sample-{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            var command = new RunCommand(stdout, stderr, TextReader.Null, verbose: false,
                () => new HostCapabilities(new FixedRandomSource(), new MapEnvironment(environment),
                    new FixedClock(FixedInstant), new FixedFileSystem(FixedFileSystem.HarnessDirectory)));

            int exitCode = command.Run(path);
            return Read(exitCode, stderr.ToString(), path);
        } finally {
            File.Delete(path);
        }
    }

    private static SampleRun Read(int exitCode, string stderr, string path) {
        var diagnostics = ErrorExampleMaster.SplitLines(stderr)
            .Select(line => HeaderPattern().Match(line))
            .Where(m => m.Success)
            .Select(m => new SampleDiagnostic(m.Groups["code"].Value,
                SubjectPattern().Match(m.Groups["message"].Value) is { Success: true } s ? s.Groups["name"].Value : null))
            .ToList();
        return new SampleRun(exitCode, diagnostics, CliDiagnosticReader.Read(stderr, path).Problems);
    }
}
