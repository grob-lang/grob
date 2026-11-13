using System.Text;

using Grob.Cli;
using Grob.Runtime;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// The internal capabilities seam on <see cref="RunCommand"/> (D-428 Decision 3): a test
/// supplies the catalogue's environment, clock and random source, and the run reads those
/// rather than the operating system's. The public constructor keeps building the OS-backed
/// set (D-343), fresh per run.
/// </summary>
public sealed class RunCommandSeamTests {
    private static readonly DateTime FixedInstant = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static HostCapabilities Capabilities(IEnvironment environment) =>
        new(new FixedRandomSource(), environment, new FixedClock(FixedInstant));

    private static string WriteScript(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        return path;
    }

    private static (string Stdout, string Stderr, int ExitCode) Run(RunCommand command, StringWriter stdout,
            StringWriter stderr, string source) {
        string path = WriteScript(source);
        try {
            int exitCode = command.Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    [Fact]
    public void Run_InjectedEnvironment_EnvGetReadsInjectedValue() {
        string key = $"GROB_SEAM_{Guid.NewGuid():N}";
        var environment = new MapEnvironment(new Dictionary<string, string> { [key] = "from-the-seam" });
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        var command = new RunCommand(stdout, stderr, TextReader.Null, verbose: false, () => Capabilities(environment));

        (string output, string errors, int exitCode) = Run(command, stdout, stderr, $"print(env.get(\"{key}\"))\n");

        Assert.Equal(0, exitCode);
        Assert.Equal("from-the-seam" + Environment.NewLine, output);
        Assert.Equal(string.Empty, errors);
        Assert.Null(Environment.GetEnvironmentVariable(key));
    }

    [Fact]
    public void Run_InjectedEnvironment_ProcessVariableIsNotRead() {
        string key = $"GROB_SEAM_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "from-the-process");
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            var command = new RunCommand(stdout, stderr, TextReader.Null, verbose: false,
                () => Capabilities(new MapEnvironment()));

            (string output, string errors, int exitCode) = Run(command, stdout, stderr, $"print(env.has(\"{key}\"))\n");

            Assert.Equal(0, exitCode);
            Assert.Equal("false" + Environment.NewLine, output);
            Assert.Equal(string.Empty, errors);
        } finally {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void Run_CapabilityFactory_InvokedOncePerRun() {
        int calls = 0;
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        var command = new RunCommand(stdout, stderr, TextReader.Null, verbose: false, () => {
            calls++;
            return Capabilities(new MapEnvironment());
        });

        Run(command, stdout, stderr, "print(1)\n");
        Run(command, stdout, stderr, "print(2)\n");

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Constructor_NullCapabilityFactory_Throws() {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            new RunCommand(TextWriter.Null, TextWriter.Null, TextReader.Null, verbose: false, null!));

        Assert.Equal("capabilities", ex.ParamName);
    }

    [Fact]
    public void PublicConstructor_ProcessVariable_EnvGetReadsIt() {
        string key = $"GROB_SEAM_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(key, "from-the-process");
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            var command = new RunCommand(stdout, stderr);

            (string output, string errors, int exitCode) = Run(command, stdout, stderr, $"print(env.get(\"{key}\"))\n");

            Assert.Equal(0, exitCode);
            Assert.Equal("from-the-process" + Environment.NewLine, output);
            Assert.Equal(string.Empty, errors);
        } finally {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void HostCapabilitiesSystem_EachCall_BuildsFreshOsBackedCapabilities() {
        HostCapabilities first = HostCapabilities.System();
        HostCapabilities second = HostCapabilities.System();

        Assert.IsType<SystemRandomSource>(first.RandomSource);
        Assert.IsType<SystemEnvironment>(first.Environment);
        Assert.IsType<SystemClock>(first.Clock);
        Assert.NotSame(first.RandomSource, second.RandomSource);
        Assert.NotSame(first.Environment, second.Environment);
        Assert.NotSame(first.Clock, second.Clock);
    }
}
