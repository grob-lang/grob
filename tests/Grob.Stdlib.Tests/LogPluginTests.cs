using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment C: <see cref="LogPlugin"/> registers <c>log.debug</c>/<c>info</c>/
/// <c>warning</c>/<c>error</c>/<c>setLevel</c> via <see cref="IGrobPlugin"/>, writing to the
/// injected <see cref="IStandardStreams"/>'s <c>Error</c> stream only when a message's own
/// level is at or above the current threshold (D-343). Chunks are hand-constructed — this
/// project has no dependency on <c>Grob.Compiler</c>.
/// </summary>
public sealed class LogPluginTests {
    private static VirtualMachine NewRegisteredVm(FakeStandardStreams streams, LogLevel initialLevel) {
        var vm = new VirtualMachine(streams);
        new LogPlugin(streams, initialLevel).Register(vm);
        return vm;
    }

    [Fact]
    public void Name_IsLog() {
        Assert.Equal("log", new LogPlugin(new FakeStandardStreams(), LogLevel.Info).Name);
    }

    [Fact]
    public void Register_AddsExactlyTheDocumentedLogMembers() {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Info);

        string[] expectedMembers = ["debug", "info", "warning", "error", "setLevel"];
        foreach (string member in expectedMembers) {
            Assert.True(vm.Globals.ContainsKey($"log.{member}"), $"missing log.{member}");
        }
        Assert.Equal(expectedMembers.Length, vm.Globals.Count);
    }

    [Fact]
    public void Register_NullStreams_Throws() {
        Assert.Throws<ArgumentNullException>(() => new LogPlugin(null!, LogLevel.Info));
    }

    // -----------------------------------------------------------------------
    // Always-emitted levels at the default (Info) threshold.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("log.info", "an info message")]
    [InlineData("log.warning", "a warning message")]
    [InlineData("log.error", "an error message")]
    public void InfoAndAbove_AtInfoThreshold_AlwaysWritesToStderr(string native, string message) {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Info);

        vm.Run(BuildCallChunk(native, Grob.Core.GrobValue.FromString(message)));

        Assert.Equal(message + Environment.NewLine, streams.Error.ToString());
    }

    [Fact]
    public void Debug_AtInfoThreshold_IsDropped_WritesNothing() {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Info);

        vm.Run(BuildCallChunk("log.debug", Grob.Core.GrobValue.FromString("hidden")));

        Assert.Equal(string.Empty, streams.Error.ToString());
    }

    [Fact]
    public void Debug_AtDebugThreshold_IsEmitted() {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Debug);

        vm.Run(BuildCallChunk("log.debug", Grob.Core.GrobValue.FromString("visible")));

        Assert.Equal("visible" + Environment.NewLine, streams.Error.ToString());
    }

    [Fact]
    public void Log_NeverWritesToStdout() {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Debug);

        vm.Run(BuildCallChunk("log.error", Grob.Core.GrobValue.FromString("x")));

        Assert.Equal(string.Empty, streams.Out.ToString());
    }

    // -----------------------------------------------------------------------
    // setLevel
    // -----------------------------------------------------------------------

    [Fact]
    public void SetLevel_Debug_ThenDebugCall_IsEmitted() {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Info);

        vm.Run(BuildCallChunk("log.setLevel", Grob.Core.GrobValue.FromString("debug")));
        vm.Run(BuildCallChunk("log.debug", Grob.Core.GrobValue.FromString("now visible")));

        Assert.Equal("now visible" + Environment.NewLine, streams.Error.ToString());
    }

    [Fact]
    public void SetLevel_Error_ThenWarningCall_IsDropped() {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Info);

        vm.Run(BuildCallChunk("log.setLevel", Grob.Core.GrobValue.FromString("error")));
        vm.Run(BuildCallChunk("log.warning", Grob.Core.GrobValue.FromString("dropped")));

        Assert.Equal(string.Empty, streams.Error.ToString());
    }

    [Theory]
    [InlineData("info")]
    [InlineData("warning")]
    public void SetLevel_RecognisesEveryLevelName_ThresholdTakesEffect(string levelName) {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Debug);

        vm.Run(BuildCallChunk("log.setLevel", Grob.Core.GrobValue.FromString(levelName)));
        vm.Run(BuildCallChunk("log.debug", Grob.Core.GrobValue.FromString("dropped")));

        // Both "info" and "warning" sit above "debug", so a debug call is now dropped —
        // proves setLevel actually parsed and applied the named level, not just accepted
        // the string without effect.
        Assert.Equal(string.Empty, streams.Error.ToString());
    }

    [Fact]
    public void SetLevel_UnrecognisedString_IsSilentNoOp_LevelUnchanged() {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Info);

        Exception? ex = Record.Exception(() =>
            vm.Run(BuildCallChunk("log.setLevel", Grob.Core.GrobValue.FromString("bogus"))));
        Assert.Null(ex);

        // Threshold is still Info: debug stays dropped, info still emits.
        vm.Run(BuildCallChunk("log.debug", Grob.Core.GrobValue.FromString("still hidden")));
        Assert.Equal(string.Empty, streams.Error.ToString());

        vm.Run(BuildCallChunk("log.info", Grob.Core.GrobValue.FromString("still visible")));
        Assert.Equal("still visible" + Environment.NewLine, streams.Error.ToString());
    }

    [Fact]
    public void SetLevel_ReturnsNil() {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Info);

        vm.Run(BuildCallChunk("log.setLevel", Grob.Core.GrobValue.FromString("debug")));

        Assert.True(vm.Stack.Peek().IsNil);
    }

    [Fact]
    public void LogCall_ReturnsNil() {
        var streams = new FakeStandardStreams();
        var vm = NewRegisteredVm(streams, LogLevel.Debug);

        vm.Run(BuildCallChunk("log.info", Grob.Core.GrobValue.FromString("x")));

        Assert.True(vm.Stack.Peek().IsNil);
    }
}
