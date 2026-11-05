using Grob.Cli;
using Grob.Runtime;
using Grob.Vm;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// D-426 Decision 2's invariant, replacing the three agreement tests: the type checker
/// and the VM resolve the same native surface from the same <see cref="NativeCatalogue"/>
/// instance. The composition root builds one per run; every global it declares is bound
/// in the VM constructed over it, and a script that resolves named-type members at
/// compile time dispatches them at run time without any second table in between.
/// </summary>
public sealed class OneCatalogueTests {
    private static string NL => Environment.NewLine;

    [Fact]
    public void BuildCatalogue_EveryGlobalIsBoundInAVmOverIt() {
        var streams = new TwoWriterStreams(TextWriter.Null, TextWriter.Null, TextReader.Null);
        NativeCatalogue catalogue = PluginRegistration.BuildCatalogue(streams, verbose: false);

        var vm = new VirtualMachine(streams, catalogue);

        Assert.Equal(catalogue.Globals.Keys.Order(StringComparer.Ordinal), vm.Globals.Keys.Order(StringComparer.Ordinal));
        Assert.All(catalogue.Globals, pair => Assert.Equal(pair.Value, vm.Globals[pair.Key]));
        Assert.Equal(["guid", "date"], catalogue.NamedTypeNames);
    }

    [Fact]
    public void Run_NamedTypeMembersResolvedByTheChecker_DispatchAtRunTime() {
        const string source = """
            id := guid.newV5(guid.namespaces.dns, "x")
            print(id.version)
            d := date.of(2026, 1, 1).addDays(1)
            print(d.toIso())
            print(d.day)
            print(d.isBefore(d.addDays(1)))
            """;
        string path = Path.Combine(Path.GetTempPath(), $"grob-one-catalogue-{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter();
            var stderr = new StringWriter();

            int exitCode = new RunCommand(stdout, stderr).Run(path);

            Assert.Equal("", stderr.ToString());
            Assert.Equal(0, exitCode);
            Assert.Equal($"5{NL}2026-01-02{NL}2{NL}true{NL}", stdout.ToString());
        } finally {
            File.Delete(path);
        }
    }
}
