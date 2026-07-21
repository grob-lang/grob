using Grob.Cli;
using Grob.Core;
using Grob.Core.PrimitiveMembers;
using Grob.Runtime;
using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// The compile-time-twin-vs-live-runtime-registration agreement test for
/// <see cref="PrimitiveMemberRegistry"/> (D-066/D-363), mirroring
/// <see cref="NamedTypeRegistryAgreementTests"/>' shape for the sibling
/// <c>NamedTypeRegistry</c> — except this diffs live <c>RegisterNative</c> qualified
/// names (the runtime signal a primitive-member native actually registers) rather than
/// <c>RegisterToString</c> type names, since primitive dispatch has no <c>ValueDisplay</c>
/// renderer hook. The "orphan" side is scoped to the <c>"string."</c> prefix so it does
/// not flag every unrelated native (<c>math.sqrt</c>, <c>date.now</c>, ...) registered by
/// the same composition-root plugin list.
/// </summary>
public sealed class PrimitiveMemberRegistryAgreementTests {
    private sealed class RecordingRegistrar : IPluginRegistrar {
        public List<string> RegisteredNativeNames { get; } = [];

        public void RegisterNative(string name, NativeFunction fn) => RegisteredNativeNames.Add(name);

        public void RegisterConstant(string name, GrobValue value) { }

        public void RegisterToString(string typeName, Func<GrobValue, string> toString) { }

        public string RenderValue(GrobValue value) => string.Empty;
    }

    private static List<string> RegisterAllAndCaptureNativeNames() {
        var registrar = new RecordingRegistrar();
        var streams = new TwoWriterStreams(TextWriter.Null, TextWriter.Null, TextReader.Null);
        PluginRegistration.RegisterAll(registrar, new SystemRandomSource(), new SystemEnvironment(), streams, verbose: false);
        return registrar.RegisteredNativeNames;
    }

    [Fact]
    public void RegisteredNativeNames_CoverEveryPrimitiveMemberRegistryEntry() {
        List<string> registered = RegisterAllAndCaptureNativeNames();

        var missing = PrimitiveMemberRegistry.AllQualifiedNativeNames.Except(registered)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0,
            $"PrimitiveMemberRegistry entries with no live RegisterNative call: {string.Join(", ", missing)}");
    }

    [Fact]
    public void RegisteredNativeNames_HaveNoOrphanStringMemberEntry() {
        List<string> registered = RegisterAllAndCaptureNativeNames();

        var orphaned = registered.Where(n => n.StartsWith("string.", StringComparison.Ordinal))
            .Except(PrimitiveMemberRegistry.AllQualifiedNativeNames)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(orphaned.Count == 0,
            $"Live 'string.*' RegisterNative calls with no PrimitiveMemberRegistry entry: {string.Join(", ", orphaned)}");
    }
}
