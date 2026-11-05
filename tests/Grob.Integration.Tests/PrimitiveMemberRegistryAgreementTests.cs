using Grob.Cli;
using Grob.Compiler;
using Grob.Core;
using Grob.Stdlib;
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
/// Sprint 9 Increment A1b (D-370)'s six <c>int.*</c>/<c>float.*</c> type-statics
/// (<c>min</c>/<c>max</c>/<c>clamp</c>) share the <c>"int."</c>/<c>"float."</c> prefix but
/// are not instance members. Since D-426 change group 5 they register with signatures
/// through the internal primitive surface (<c>RegisterPrimitiveStatic</c>), which this
/// public-surface recording registrar never sees, so the orphan check needs no exclusion
/// for them.
/// </para>
/// </summary>
public sealed class PrimitiveMemberRegistryAgreementTests {
    private sealed class RecordingRegistrar : IPluginRegistrar {
        public List<string> RegisteredNativeNames { get; } = [];

        public void RegisterNative(string name, NativeFunction fn) => RegisteredNativeNames.Add(name);

        public void RegisterNative(FunctionSignature signature, NativeFunction fn) => RegisteredNativeNames.Add(signature.Name);

        public void RegisterNamedType(NamedTypeDefinition definition) { }

        public void RegisterConstant(string name, GrobValue value) { }

        public string RenderValue(GrobValue value) => string.Empty;
    }

    private static List<string> RegisterAllAndCaptureNativeNames() {
        var registrar = new RecordingRegistrar();
        var streams = new TwoWriterStreams(TextWriter.Null, TextWriter.Null, TextReader.Null);
        foreach (IGrobPlugin plugin in StdlibPlugins.Create(
                new SystemRandomSource(), new SystemEnvironment(), streams, new SystemClock(), verbose: false)) {
            plugin.Register(registrar);
        }
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
            .OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(orphaned.Count == 0,
            $"Live '{prefix}*' RegisterNative calls with no PrimitiveMemberRegistry entry: {string.Join(", ", orphaned)}");
    }
}
