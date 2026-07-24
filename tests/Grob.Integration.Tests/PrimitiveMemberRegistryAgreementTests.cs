using Grob.Cli;
using Grob.Compiler;
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
/// renderer hook. The "orphan" side is scoped to the <c>"string."</c>/<c>"int."</c>/
/// <c>"float."</c>/<c>"bool."</c> prefixes (Sprint 9 Increment A1a/D-369 added the latter
/// three) so it does not flag every unrelated native (<c>math.sqrt</c>, <c>date.now</c>,
/// ...) registered by the same composition-root plugin list.
/// <para>
/// Sprint 9 Increment A1b (D-370) registered six <c>int.*</c>/<c>float.*</c> natives
/// (<c>min</c>/<c>max</c>/<c>clamp</c>) through the sibling <c>NamespaceRegistry</c>
/// (<c>Grob.Compiler</c>) instead — a genuinely different compile-time registry for a
/// namespace-receiver call, not an instance method, even though the qualified native
/// name shares the same <c>"int."</c>/<c>"float."</c> prefix D-369 already claimed. Left
/// unreconciled, the orphan check below would misreport them as drift (a live native
/// with no <see cref="PrimitiveMemberRegistry"/> entry) even though they are correctly
/// homed elsewhere — the orphan computation excludes anything
/// <see cref="NamespaceRegistry.AllQualifiedNativeNames"/> already accounts for, so a
/// genuine orphan (owned by neither registry) still fails the check, while a legitimately
/// dual-prefix, single-registry-owned name like <c>int.min</c> does not.
/// </para>
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

    [Theory]
    [InlineData("string.")]
    [InlineData("int.")]
    [InlineData("float.")]
    [InlineData("bool.")]
    public void RegisteredNativeNames_HaveNoOrphanPrimitiveMemberEntry(string prefix) {
        List<string> registered = RegisterAllAndCaptureNativeNames();

        var orphaned = registered.Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .Except(PrimitiveMemberRegistry.AllQualifiedNativeNames)
            .Except(NamespaceRegistry.AllQualifiedNativeNames)
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(orphaned.Count == 0,
            $"Live '{prefix}*' RegisterNative calls with no PrimitiveMemberRegistry entry: {string.Join(", ", orphaned)}");
    }
}
