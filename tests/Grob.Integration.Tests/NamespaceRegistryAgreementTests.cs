using Grob.Cli;
using Grob.Compiler;
using Grob.Core;
using Grob.Runtime;
using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// The compile-time-twin-vs-live-runtime-registration coverage-direction agreement test
/// for <see cref="NamespaceRegistry"/> (D-342), mirroring
/// <see cref="PrimitiveMemberRegistryAgreementTests"/>' shape for the sibling
/// <c>PrimitiveMemberRegistry</c>. Only the coverage direction is checked here — every
/// declared <see cref="NamespaceRegistry.NativeMember"/> entry (across every namespace,
/// not only <c>int</c>/<c>float</c>) has a live <c>RegisterNative</c> call from the
/// composition-root plugin list — not the reverse orphan direction, which
/// <see cref="PrimitiveMemberRegistryAgreementTests"/> already covers for the
/// <c>"int."</c>/<c>"float."</c> prefixes these two registries now share. A generic
/// all-namespaces orphan check would additionally need to special-case
/// <c>formatAs</c> (its three members are deliberately unmodelled in
/// <c>NamespaceRegistry</c> — see the comment at <c>NamespaceRegistry.cs</c>'s
/// <c>"formatAs"</c> entry) — out of scope for Sprint 9 Increment A1b (D-370), which adds
/// only the <c>int.min</c>/<c>.max</c>/<c>.clamp</c> and
/// <c>float.min</c>/<c>.max</c>/<c>.clamp</c> entries this test now covers as a free side
/// effect of diffing the whole table.
/// </summary>
public sealed class NamespaceRegistryAgreementTests {
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
    public void RegisteredNativeNames_CoverEveryNamespaceRegistryNativeMemberEntry() {
        List<string> registered = RegisterAllAndCaptureNativeNames();

        var missing = NamespaceRegistry.AllQualifiedNativeNames.Except(registered)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0,
            $"NamespaceRegistry NativeMember entries with no live RegisterNative call: {string.Join(", ", missing)}");
    }
}
