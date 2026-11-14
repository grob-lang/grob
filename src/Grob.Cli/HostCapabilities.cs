using Grob.Runtime;

namespace Grob.Cli;

/// <summary>
/// The capabilities a run's <see cref="NativeCatalogue"/> is built over (D-343): its random
/// source, environment, clock and file system. <see cref="RunCommand"/> takes a factory for
/// them through its internal constructor, so the error-examples harness can run the CLI's
/// own pipeline over synthetic capabilities instead of the operating system's (D-428
/// Decision 3).
/// </summary>
/// <param name="RandomSource">The source <c>math.random*</c> and <c>guid.newV4</c> draw from.</param>
/// <param name="Environment">The variables the <c>env</c> module reads and writes.</param>
/// <param name="Clock">The wall clock and local zone <c>date</c> and <c>guid.newV7</c> read.</param>
/// <param name="FileSystem">The current directory <c>path.resolve</c> anchors to (R-32).</param>
internal sealed record HostCapabilities(
        IRandomSource RandomSource, IEnvironment Environment, IClock Clock, IFileSystem FileSystem) {
    /// <summary>
    /// The OS-backed set, built fresh on every call so one run's <c>math.randomSeed</c>
    /// never leaks into the next.
    /// </summary>
    internal static HostCapabilities System() =>
        new(new SystemRandomSource(), new SystemEnvironment(), new SystemClock(), new SystemFileSystem());
}
