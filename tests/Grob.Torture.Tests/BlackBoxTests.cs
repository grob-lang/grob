using Grob.Torture.Campaign;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The black-box check reads what a user would see from a reproducer through the real CLI: E9001
/// and no stack trace, or the strategy's §1 failure, a .NET frame or the unhandled-exception banner.
/// </summary>
public sealed class BlackBoxTests {
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
