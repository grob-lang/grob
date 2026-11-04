using System.Diagnostics.CodeAnalysis;

using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// Capability implementations for a compile-only catalogue
/// (<see cref="StdlibCatalogue.ForCompileOnly"/>): every member throws
/// <see cref="InvalidOperationException"/>, so a native that reaches a capability while
/// only type checking runs fails loudly instead of reading the host's clock, environment
/// or streams. Plugin constructors only null-check and store these (gate report §1.3).
/// </summary>
internal static class InertCapabilities {
    /// <summary>A random source that throws on every draw.</summary>
    internal static IRandomSource RandomSource { get; } = new InertRandomSource();

    /// <summary>An environment that throws on every read and write.</summary>
    internal static IEnvironment Environment { get; } = new InertEnvironment();

    /// <summary>Standard streams that throw when any stream is requested.</summary>
    internal static IStandardStreams Streams { get; } = new InertStreams();

    /// <summary>A clock that throws when read.</summary>
    internal static IClock Clock { get; } = new InertClock();

    private const string RandomSourceName = "random source";
    private const string EnvironmentName = "environment";

    private static InvalidOperationException Refuse(string capability) =>
        new($"The compile-only catalogue has no {capability}; a native cannot use one while only type checking runs.");

    private sealed class InertRandomSource : IRandomSource {
        public double NextDouble() => throw Refuse(RandomSourceName);

        public long NextInt(long min, long max) => throw Refuse(RandomSourceName);

        public void Reseed(long seed) => throw Refuse(RandomSourceName);
    }

    private sealed class InertEnvironment : IEnvironment {
        public string? Get(string key) => throw Refuse(EnvironmentName);

        public void Set(string key, string value) => throw Refuse(EnvironmentName);

        [ExcludeFromCodeCoverage(Justification =
            "env.has resolves through Get, so no native calls Has; unreachable through the catalogue, " +
            "retained as belt-and-braces so a future native that calls Has still fails loudly.")]
        public bool Has(string key) => throw Refuse(EnvironmentName);

        public IReadOnlyDictionary<string, string> All() => throw Refuse(EnvironmentName);
    }

    private sealed class InertStreams : IStandardStreams {
        public TextWriter Out => throw Refuse("standard output");

        public TextWriter Error => throw Refuse("standard error");

        [ExcludeFromCodeCoverage(Justification =
            "No native reads standard input without first writing its prompt to standard output, " +
            "and Out throws before In is reached; unreachable through the catalogue, retained as " +
            "belt-and-braces so a future native that reads first still fails loudly.")]
        public TextReader In => throw Refuse("standard input");
    }

    private sealed class InertClock : IClock {
        public DateTime UtcNow => throw Refuse("clock");
    }
}
