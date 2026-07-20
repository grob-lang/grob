using Grob.Cli;
using Grob.Core;
using Grob.Core.NamedTypes;
using Grob.Runtime;
using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// The compile-time-twin-vs-live-runtime-registration agreement test for
/// <see cref="NamedTypeRegistry"/> (D-356), mirroring the two-independently-derived-sets
/// symmetric-diff shape <c>ErrorCatalogAgreementTests</c> (D-308) uses for
/// <c>ErrorCatalog</c> — applied here to a live runtime registration event rather than a
/// markdown document, since no test comparing <c>NamespaceRegistry</c> against live
/// <see cref="IGrobPlugin"/> registration exists yet to copy (D-342 describes that shape
/// but it was never built). Drives the real composition-root plugin list
/// (<see cref="PluginRegistration.RegisterAll"/>) through a recording
/// <see cref="IPluginRegistrar"/> that captures every <c>RegisterToString</c> type name —
/// the runtime signal a nominal type's instance surface actually registers — and diffs
/// it both ways against <see cref="NamedTypeRegistry.Names"/>. Lives here (not a
/// dedicated <c>Grob.Cli.Tests</c>, which does not exist) because
/// <c>Grob.Integration.Tests</c> already references <c>Grob.Cli</c> and already holds
/// <c>InternalsVisibleTo</c> from it — the only test project that can reach both
/// <see cref="PluginRegistration"/> and <see cref="NamedTypeRegistry"/> without new
/// project-reference wiring.
/// </summary>
public sealed class NamedTypeRegistryAgreementTests {
    private sealed class RecordingRegistrar : IPluginRegistrar {
        public List<string> RegisteredToStringTypeNames { get; } = [];

        public void RegisterNative(string name, NativeFunction fn) { }

        public void RegisterConstant(string name, GrobValue value) { }

        public void RegisterToString(string typeName, Func<GrobValue, string> toString) =>
            RegisteredToStringTypeNames.Add(typeName);

        public string RenderValue(GrobValue value) => string.Empty;
    }

    private static List<string> RegisterAllAndCaptureToStringTypeNames() {
        var registrar = new RecordingRegistrar();
        var streams = new TwoWriterStreams(TextWriter.Null, TextWriter.Null, TextReader.Null);
        PluginRegistration.RegisterAll(registrar, new SystemRandomSource(), new SystemEnvironment(), streams, verbose: false);
        return registrar.RegisteredToStringTypeNames;
    }

    [Fact]
    public void RegisteredToStringTypeNames_CoverEveryNamedTypeRegistryEntry() {
        List<string> registered = RegisterAllAndCaptureToStringTypeNames();

        var missing = NamedTypeRegistry.Names.Except(registered).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0,
            $"NamedTypeRegistry entries with no live RegisterToString call: {string.Join(", ", missing)}");
    }

    [Fact]
    public void RegisteredToStringTypeNames_HaveNoOrphanNamedTypeEntry() {
        List<string> registered = RegisterAllAndCaptureToStringTypeNames();

        var orphaned = registered.Except(NamedTypeRegistry.Names).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(orphaned.Count == 0,
            $"Live RegisterToString calls with no NamedTypeRegistry entry: {string.Join(", ", orphaned)}");
    }
}
