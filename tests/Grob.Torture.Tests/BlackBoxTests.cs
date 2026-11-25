using Grob.Runtime;
using Grob.Stdlib;
using Grob.Torture.Campaign;
using Grob.Torture.Fakes;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The black-box check reads what a user would see from a reproducer through the real CLI: E9001
/// and no stack trace, or the strategy's §1 failure, a .NET frame or the unhandled-exception banner.
/// </summary>
public sealed class BlackBoxTests {
    // The black-box check runs mutated reproducers through the real CLI, with the machine's own
    // capabilities and no isolation (R-95). That is safe only while nothing the CLI registers can
    // write or delete a file, start a process or reach the network. Each namespace and named type
    // listed here was judged to have no effect outside the child process: env.set changes the
    // child's own environment. A new one fails this test until it is judged the same way; one that
    // touches the machine, such as 9C's fs, needs R-95 closed first.
    private static readonly string[] NamespacesWithoutHostEffects =
        ["date", "env", "float", "formatAs", "guid", "int", "log", "math", "path", "regex", "strings"];

    private static readonly string[] NamedTypesWithoutHostEffects = ["Match", "Regex", "date", "guid"];

    [Fact]
    public void RealCatalogue_RegistersOnlyWhatTheBlackBoxCheckMayRunUnisolated() {
        HostFakes fakes = HostFakes.Create();
        NativeCatalogue catalogue = StdlibCatalogue.Build(fakes.RandomSource, fakes.Environment, fakes.Streams, fakes.Clock, fakes.FileSystem, verbose: false);

        Assert.Equal(NamespacesWithoutHostEffects.Order(StringComparer.Ordinal), catalogue.NamespaceNames.Order(StringComparer.Ordinal));
        Assert.Equal(NamedTypesWithoutHostEffects.Order(StringComparer.Ordinal), catalogue.NamedTypeNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Classify_E9001WithNoFrame_IsReported() {
        var output = new GrobOutput(1,
            "error[E9001]: internal compiler error — please report\n  --> r60.grob (while running)\n  = GrobInternalException: boom\n", false);

        Assert.Equal(BlackBoxVerdict.Reported, BlackBox.Classify(output));
    }

    [Theory]
    [InlineData("error[E9001]: internal compiler error\n   at Grob.Vm.VirtualMachine.Run(Chunk chunk)\n")]
    [InlineData("Unhandled exception. System.OverflowException: Arithmetic operation resulted in an overflow.\n")]
    [InlineData("Stack overflow.\n   at Grob.Compiler.Parser.ParseUnary()\n")]
    public void Classify_ADotNetFrameOrTheBanner_IsAStackTrace(string stderr) {
        Assert.Equal(BlackBoxVerdict.StackTrace, BlackBox.Classify(new GrobOutput(-532462766, stderr, false)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("error[E5001]: integer overflow\n  --> x.grob:1:1\n")]
    [InlineData("note: at this line the script stopped\n")]
    public void Classify_NeitherE9001NorAFrame_IsNotReproduced(string stderr) {
        Assert.Equal(BlackBoxVerdict.NotReproduced, BlackBox.Classify(new GrobOutput(1, stderr, false)));
    }

    [Fact]
    public void Classify_KilledAtTheTimeout_IsTimedOut() {
        Assert.Equal(BlackBoxVerdict.TimedOut, BlackBox.Classify(new GrobOutput(-1, "error[E9001]", true)));
    }
}
