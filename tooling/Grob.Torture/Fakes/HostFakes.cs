using Grob.Runtime;

namespace Grob.Torture.Fakes;

/// <summary>
/// The capabilities one run's catalogue is built over. The oracle never touches the real file
/// system, environment, clock, random source or console: every ambient interface in
/// <c>Grob.Runtime</c> is a fake here (prompt §1.4, gate report §4.2).
/// </summary>
/// <param name="RandomSource">The source <c>math.random*</c> and <c>guid.newV4</c> draw from.</param>
/// <param name="Environment">The variables <c>env</c> reads and writes.</param>
/// <param name="Clock">The clock and zone <c>date</c> reads.</param>
/// <param name="FileSystem">The current directory <c>path.resolve</c> anchors to.</param>
/// <param name="Streams">The streams <c>print</c>, <c>input</c> and <c>log</c> use.</param>
internal sealed record HostFakes(
        IRandomSource RandomSource, IEnvironment Environment, IClock Clock, IFileSystem FileSystem, FakeStreams Streams) {
    /// <summary>The seed every run's random source starts from, so a run is reproducible.</summary>
    public const long RandomSeed = 0x5EED;

    /// <summary>A fresh set: nothing one run does is seen by the next.</summary>
    public static HostFakes Create() =>
        new(new SplitMixRandomSource(RandomSeed), new InMemoryEnvironment(), new FixedClock(), new FixedFileSystem(), new FakeStreams());
}
