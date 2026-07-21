using Grob.Core;
using Grob.Core.PrimitiveMembers;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 9 — <see cref="StringMethodsPlugin"/> registers the <c>string</c> instance
/// surface's runtime natives (D-066's compile-time-sugar model; <c>PrimitiveMemberRegistry</c>
/// is the compile-time twin). Every native takes the receiver as its first argument (the
/// compiler injects it — <see cref="Compiler.Compiler"/> is not referenced here, this
/// project has no dependency on <c>Grob.Compiler</c>), so a hand-built
/// <see cref="ChunkBuilders.BuildCallChunk"/> call with the receiver as <c>args[0]</c>
/// exercises the exact shape the compiler emits. End to end through a real
/// <see cref="VirtualMachine"/>.
/// </summary>
public sealed class StringMethodsPluginTests {
    private static VirtualMachine NewRegisteredVm() {
        var vm = new VirtualMachine(new StringWriter());
        new StringMethodsPlugin().Register(vm);
        return vm;
    }

    [Fact]
    public void Name_IsString() {
        Assert.Equal("string", new StringMethodsPlugin().Name);
    }

    [Fact]
    public void Register_AddsExactlyTheTwentyOneQualifiedNatives() {
        var vm = new VirtualMachine(new StringWriter());
        new StringMethodsPlugin().Register(vm);

        foreach (string name in PrimitiveMemberRegistry.AllQualifiedNativeNames) {
            Assert.True(vm.Globals.ContainsKey(name), $"missing native registration: {name}");
        }
        Assert.Equal(21, vm.Globals.Count);
    }

    // -----------------------------------------------------------------------
    // Properties
    // -----------------------------------------------------------------------

    [Fact]
    public void Length_ReturnsCharacterCount() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.length", GrobValue.FromString("hello")));
        Assert.Equal(GrobValue.FromInt(5), vm.Stack.Peek());
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("x", false)]
    public void IsEmpty_ReflectsLength(string input, bool expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.isEmpty", GrobValue.FromString(input)));
        Assert.Equal(GrobValue.FromBool(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // toInt / toFloat — nil on parse failure.
    // -----------------------------------------------------------------------

    [Fact]
    public void ToInt_ValidNumeric_ReturnsInt() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.toInt", GrobValue.FromString("42")));
        Assert.Equal(GrobValue.FromInt(42), vm.Stack.Peek());
    }

    [Fact]
    public void ToInt_Unparseable_ReturnsNil() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.toInt", GrobValue.FromString("nope")));
        Assert.True(vm.Stack.Peek().IsNil);
    }

    [Fact]
    public void ToFloat_ValidNumeric_ReturnsFloat() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.toFloat", GrobValue.FromString("3.5")));
        Assert.Equal(GrobValue.FromFloat(3.5), vm.Stack.Peek());
    }

    [Fact]
    public void ToFloat_Unparseable_ReturnsNil() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.toFloat", GrobValue.FromString("nope")));
        Assert.True(vm.Stack.Peek().IsNil);
    }

    // -----------------------------------------------------------------------
    // trim / trimStart / trimEnd / upper / lower
    // -----------------------------------------------------------------------

    [Fact]
    public void Trim_RemovesLeadingAndTrailingWhitespace() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.trim", GrobValue.FromString("  hi  ")));
        Assert.Equal(GrobValue.FromString("hi"), vm.Stack.Peek());
    }

    [Fact]
    public void TrimStart_RemovesLeadingWhitespaceOnly() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.trimStart", GrobValue.FromString("  hi  ")));
        Assert.Equal(GrobValue.FromString("hi  "), vm.Stack.Peek());
    }

    [Fact]
    public void TrimEnd_RemovesTrailingWhitespaceOnly() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.trimEnd", GrobValue.FromString("  hi  ")));
        Assert.Equal(GrobValue.FromString("  hi"), vm.Stack.Peek());
    }

    [Fact]
    public void Upper_UppercasesEveryCharacter() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.upper", GrobValue.FromString("Hi")));
        Assert.Equal(GrobValue.FromString("HI"), vm.Stack.Peek());
    }

    [Fact]
    public void Lower_LowercasesEveryCharacter() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.lower", GrobValue.FromString("Hi")));
        Assert.Equal(GrobValue.FromString("hi"), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // split / contains / startsWith / endsWith / replace
    // -----------------------------------------------------------------------

    [Fact]
    public void Split_BySeparator_ReturnsArrayOfParts() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.split", GrobValue.FromString("a|b|c"), GrobValue.FromString("|")));

        Assert.True(vm.Stack.Peek().TryAsArray(out GrobArray? array));
        Assert.Equal(["a", "b", "c"], array!.Elements.Select(e => e.AsString()));
    }

    [Theory]
    [InlineData("abc", "b", true)]
    [InlineData("abc", "z", false)]
    public void Contains_ReflectsSubstringPresence(string input, string needle, bool expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.contains", GrobValue.FromString(input), GrobValue.FromString(needle)));
        Assert.Equal(GrobValue.FromBool(expected), vm.Stack.Peek());
    }

    [Theory]
    [InlineData("abc", "a", true)]
    [InlineData("abc", "b", false)]
    public void StartsWith_ReflectsPrefix(string input, string prefix, bool expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.startsWith", GrobValue.FromString(input), GrobValue.FromString(prefix)));
        Assert.Equal(GrobValue.FromBool(expected), vm.Stack.Peek());
    }

    [Theory]
    [InlineData("abc", "c", true)]
    [InlineData("abc", "b", false)]
    public void EndsWith_ReflectsSuffix(string input, string suffix, bool expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.endsWith", GrobValue.FromString(input), GrobValue.FromString(suffix)));
        Assert.Equal(GrobValue.FromBool(expected), vm.Stack.Peek());
    }

    [Fact]
    public void Replace_ReplacesEveryOccurrence() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk(
            "string.replace", GrobValue.FromString("a-a-a"), GrobValue.FromString("a"), GrobValue.FromString("z")));
        Assert.Equal(GrobValue.FromString("z-z-z"), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // indexOf / lastIndexOf — -1 when absent.
    // -----------------------------------------------------------------------

    [Fact]
    public void IndexOf_Found_ReturnsFirstOccurrenceIndex() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.indexOf", GrobValue.FromString("abcabc"), GrobValue.FromString("b")));
        Assert.Equal(GrobValue.FromInt(1), vm.Stack.Peek());
    }

    [Fact]
    public void IndexOf_NotFound_ReturnsMinusOne() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.indexOf", GrobValue.FromString("abc"), GrobValue.FromString("z")));
        Assert.Equal(GrobValue.FromInt(-1), vm.Stack.Peek());
    }

    [Fact]
    public void LastIndexOf_Found_ReturnsLastOccurrenceIndex() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.lastIndexOf", GrobValue.FromString("abcabc"), GrobValue.FromString("b")));
        Assert.Equal(GrobValue.FromInt(4), vm.Stack.Peek());
    }

    [Fact]
    public void LastIndexOf_NotFound_ReturnsMinusOne() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.lastIndexOf", GrobValue.FromString("abc"), GrobValue.FromString("z")));
        Assert.Equal(GrobValue.FromInt(-1), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // substring / left / right — IndexError out of range.
    // -----------------------------------------------------------------------

    [Fact]
    public void Substring_ValidRange_ReturnsSlice() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.substring", GrobValue.FromString("hello"), GrobValue.FromInt(1), GrobValue.FromInt(3)));
        Assert.Equal(GrobValue.FromString("ell"), vm.Stack.Peek());
    }

    [Fact]
    public void Substring_OutOfRange_ThrowsIndexError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.substring", GrobValue.FromString("hi"), GrobValue.FromInt(1), GrobValue.FromInt(5))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Substring_StartOverflowsOnAddition_StillThrowsIndexError() {
        // start + length must not wrap: long.MaxValue + 1 overflows to a negative value that
        // would bypass an `start + length > Length` guard, letting Substring throw an uncoded
        // CLR fault instead of E5101. The guard checks start against Length before subtracting.
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.substring", GrobValue.FromString("hi"),
                GrobValue.FromInt(long.MaxValue), GrobValue.FromInt(1))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Repeat_RepeatsStringNTimes() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.repeat", GrobValue.FromString("ab"), GrobValue.FromInt(3)));
        Assert.Equal(GrobValue.FromString("ababab"), vm.Stack.Peek());
    }

    [Fact]
    public void Left_ValidN_ReturnsPrefix() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.left", GrobValue.FromString("hello"), GrobValue.FromInt(3)));
        Assert.Equal(GrobValue.FromString("hel"), vm.Stack.Peek());
    }

    [Fact]
    public void Left_NTooLarge_ThrowsIndexError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.left", GrobValue.FromString("hi"), GrobValue.FromInt(5))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Right_ValidN_ReturnsSuffix() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.right", GrobValue.FromString("hello"), GrobValue.FromInt(3)));
        Assert.Equal(GrobValue.FromString("llo"), vm.Stack.Peek());
    }

    [Fact]
    public void Right_NTooLarge_ThrowsIndexError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.right", GrobValue.FromString("hi"), GrobValue.FromInt(5))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void ToString_ReturnsReceiverUnchanged() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.toString", GrobValue.FromString("hi")));
        Assert.Equal(GrobValue.FromString("hi"), vm.Stack.Peek());
    }
}
