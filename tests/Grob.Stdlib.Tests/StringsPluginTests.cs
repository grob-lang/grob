using Grob.Core;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment B: <see cref="StringsPlugin"/> registers the module's one function,
/// <c>strings.join(parts: string[], separator: string): string</c> (D-342) — every other
/// string operation is an instance method on the <c>string</c> type, out of scope here.
/// End to end through a real <see cref="VirtualMachine"/>; chunks are hand constructed —
/// this project has no dependency on <c>Grob.Compiler</c>.
/// </summary>
public sealed class StringsPluginTests {
    private static VirtualMachine NewRegisteredVm() {
        var vm = new VirtualMachine(new StringWriter());
        new StringsPlugin().Register(vm);
        return vm;
    }

    [Fact]
    public void Name_IsStrings() {
        Assert.Equal("strings", new StringsPlugin().Name);
    }

    [Fact]
    public void Register_AddsExactlyStringsJoin() {
        var vm = new VirtualMachine(new StringWriter());
        new StringsPlugin().Register(vm);

        Assert.True(vm.Globals.ContainsKey("strings.join"));
        Assert.Equal(1, vm.Globals.Count);
    }

    [Fact]
    public void Join_ThreeElements_JoinsWithSeparator() {
        var vm = NewRegisteredVm();
        var parts = new GrobArray([
            GrobValue.FromString("Alice"), GrobValue.FromString("Bob"), GrobValue.FromString("Charlie"),
        ]);

        vm.Run(BuildCallChunk("strings.join", GrobValue.FromArray(parts), GrobValue.FromString(", ")));

        Assert.Equal(GrobValue.FromString("Alice, Bob, Charlie"), vm.Stack.Peek());
    }

    [Fact]
    public void Join_EmptyArray_ReturnsEmptyString() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("strings.join", GrobValue.FromArray(new GrobArray()), GrobValue.FromString(", ")));

        Assert.Equal(GrobValue.FromString(""), vm.Stack.Peek());
    }

    [Fact]
    public void Join_SingleElement_ReturnsItUnchanged_SeparatorNeverApplied() {
        var vm = NewRegisteredVm();
        var parts = new GrobArray([GrobValue.FromString("only")]);

        vm.Run(BuildCallChunk("strings.join", GrobValue.FromArray(parts), GrobValue.FromString(", ")));

        Assert.Equal(GrobValue.FromString("only"), vm.Stack.Peek());
    }
}
