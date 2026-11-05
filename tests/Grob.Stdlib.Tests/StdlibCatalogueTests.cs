using Grob.Core;
using Grob.Runtime;
using Grob.Vm;

using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Tests for <see cref="StdlibPlugins"/> and <see cref="StdlibCatalogue"/> (D-426
/// Decision 2, gate report §1.3 and §1.4.2): the host builds one catalogue from the
/// twelve first-party plugins, and a compile-only host builds it over inert
/// capabilities, which works because no plugin touches a capability at registration.
/// A native that does touch one at call time fails loudly rather than reading the
/// clock, the environment or a stream.
/// </summary>
public sealed class StdlibCatalogueTests {
    private static readonly string[] ExpectedOrder = [
        "math", "path", "strings", "env", "log", "io", "guid", "date", "formatAs", "string", "numeric", "numericStatics",
    ];

    private static IReadOnlyList<IGrobPlugin> CreateWithFakes(bool verbose = false) =>
        StdlibPlugins.Create(
            new TestRandomSource(1), new FakeEnvironment(), new FakeStandardStreams(), new TestClock(new DateTime(2026, 1, 1)), verbose);

    [Fact]
    public void Create_ReturnsTheTwelvePluginsInRegistrationOrder() =>
        Assert.Equal(ExpectedOrder, CreateWithFakes().Select(p => p.Name));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Create_VerboseSelectsTheDebugThreshold(bool verbose, bool debugWritten) {
        var error = new StringWriter();
        IGrobPlugin log = StdlibPlugins.Create(
                new TestRandomSource(1), new FakeEnvironment(), new FakeStandardStreams(error: error), new TestClock(new DateTime(2026, 1, 1)), verbose)
            .Single(p => p.Name == "log");
        NativeCatalogue catalogue = new NativeCatalogueBuilder().Add(log).Build();
        var debug = (NativeFunction)catalogue.Globals["log.debug"].AsFunction();

        debug.Implementation([GrobValue.FromString("probe")], default);

        Assert.Equal(debugWritten, error.ToString().Contains("probe", StringComparison.Ordinal));
    }

    [Fact]
    public void Create_NullArguments_Throw() {
        var random = new TestRandomSource(1);
        var env = new FakeEnvironment();
        var streams = new FakeStandardStreams();
        var clock = new TestClock(new DateTime(2026, 1, 1));

        Assert.Throws<ArgumentNullException>(() => StdlibPlugins.Create(null!, env, streams, clock, false));
        Assert.Throws<ArgumentNullException>(() => StdlibPlugins.Create(random, null!, streams, clock, false));
        Assert.Throws<ArgumentNullException>(() => StdlibPlugins.Create(random, env, null!, clock, false));
        Assert.Throws<ArgumentNullException>(() => StdlibPlugins.Create(random, env, streams, null!, false));
    }

    [Fact]
    public void Build_LoadsEveryGlobalIntoAVmConstructedOverIt() {
        NativeCatalogue catalogue = StdlibCatalogue.Build(
            new TestRandomSource(1), new FakeEnvironment(), new FakeStandardStreams(), new TestClock(new DateTime(2026, 1, 1)), verbose: false);

        var vm = new VirtualMachine(new StringWriter(), catalogue);

        Assert.Equal(catalogue.Globals.Keys.Order(StringComparer.Ordinal), vm.Globals.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(["guid", "date"], catalogue.NamedTypeNames);
    }

    [Fact]
    public void Build_RendersThroughThePluginRenderers() {
        NativeCatalogue catalogue = StdlibCatalogue.Build(
            new TestRandomSource(1), new FakeEnvironment(), new FakeStandardStreams(), new TestClock(new DateTime(2026, 1, 1)), verbose: false);

        Assert.Equal("00000000-0000-0000-0000-000000000000", catalogue.RenderValue(catalogue.Globals["guid.empty"]));
    }

    [Fact]
    public void ForCompileOnly_BuildsTheSameGlobalSetAsARealBuild() {
        NativeCatalogue real = StdlibCatalogue.Build(
            new TestRandomSource(1), new FakeEnvironment(), new FakeStandardStreams(), new TestClock(new DateTime(2026, 1, 1)), verbose: false);

        NativeCatalogue compileOnly = StdlibCatalogue.ForCompileOnly();

        Assert.Equal(real.Globals.Keys, compileOnly.Globals.Keys);
        Assert.Equal(real.NamespaceNames, compileOnly.NamespaceNames);
        Assert.Equal(real.NamedTypeNames, compileOnly.NamedTypeNames);
    }

    [Fact]
    public void ForCompileOnly_ReturnsAFreshCatalogueEachCall() =>
        Assert.NotSame(StdlibCatalogue.ForCompileOnly(), StdlibCatalogue.ForCompileOnly());

    [Theory]
    [InlineData("math.random")]
    [InlineData("env.all")]
    [InlineData("date.now")]
    [InlineData("guid.newV4")]
    public void ForCompileOnly_NativeThatReadsACapability_Throws(string name) {
        NativeCatalogue catalogue = StdlibCatalogue.ForCompileOnly();
        NativeFunction native = catalogue.Globals[name].AsFunction() as NativeFunction
            ?? throw new InvalidOperationException($"{name} is not a native.");

        var ex = Assert.Throws<InvalidOperationException>(() => native.Implementation([], default));

        Assert.Contains("compile-only", ex.Message);
    }

    [Theory]
    [InlineData("math.randomSeed", 7L)]
    [InlineData("env.get", "HOME")]
    [InlineData("env.has", "HOME")]
    [InlineData("env.set", "HOME", "/tmp")]
    public void ForCompileOnly_NativeThatTakesArgumentsAndReadsACapability_Throws(string name, params object[] args) {
        NativeCatalogue catalogue = StdlibCatalogue.ForCompileOnly();
        var native = (NativeFunction)catalogue.Globals[name].AsFunction();
        GrobValue[] values = [.. args.Select(a => a is long l ? GrobValue.FromInt(l) : GrobValue.FromString((string)a))];

        var ex = Assert.Throws<InvalidOperationException>(() => native.Implementation(values, default));

        Assert.Contains("compile-only", ex.Message);
    }

    [Fact]
    public void ForCompileOnly_NativeThatWritesAStream_Throws() {
        NativeCatalogue catalogue = StdlibCatalogue.ForCompileOnly();
        var native = (NativeFunction)catalogue.Globals["log.error"].AsFunction();

        var ex = Assert.Throws<InvalidOperationException>(() => native.Implementation([GrobValue.FromString("x")], default));

        Assert.Contains("compile-only", ex.Message);
    }

    [Fact]
    public void ForCompileOnly_NativeThatReadsStdin_Throws() {
        NativeCatalogue catalogue = StdlibCatalogue.ForCompileOnly();
        var native = (NativeFunction)catalogue.Globals["input"].AsFunction();

        var ex = Assert.Throws<InvalidOperationException>(() => native.Implementation([GrobValue.FromString("")], default));

        Assert.Contains("compile-only", ex.Message);
    }

    [Fact]
    public void ForCompileOnly_PureNative_StillRuns() {
        NativeCatalogue catalogue = StdlibCatalogue.ForCompileOnly();
        var native = (NativeFunction)catalogue.Globals["math.sqrt"].AsFunction();

        Assert.Equal(GrobValue.FromFloat(3.0), native.Implementation([GrobValue.FromFloat(9.0)], default));
    }
}
