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
    public void Register_AddsExactlyTheTwentyFourQualifiedNatives() {
        var vm = new VirtualMachine(new StringWriter());
        new StringMethodsPlugin().Register(vm);

        foreach (string name in PrimitiveMemberRegistry.AllQualifiedNativeNames) {
            Assert.True(vm.Globals.ContainsKey(name), $"missing native registration: {name}");
        }
        Assert.Equal(24, vm.Globals.Count);
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
    public void Substring_LengthAtIntMaxValue_StillThrowsIndexErrorNotHostException() {
        // Locks in the audit finding: Substring is already safe at int.MaxValue, bounded
        // by explicit comparisons against s.Length rather than any wrap-prone arithmetic.
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.substring", GrobValue.FromString("hi"),
                GrobValue.FromInt(0), GrobValue.FromInt(int.MaxValue))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Substring_NegativeStart_ThrowsIndexError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.substring", GrobValue.FromString("hi"),
                GrobValue.FromInt(-1), GrobValue.FromInt(1))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Substring_NegativeLength_ThrowsIndexError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.substring", GrobValue.FromString("hi"),
                GrobValue.FromInt(0), GrobValue.FromInt(-1))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Repeat_RepeatsStringNTimes() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.repeat", GrobValue.FromString("ab"), GrobValue.FromInt(3)));
        Assert.Equal(GrobValue.FromString("ababab"), vm.Stack.Peek());
    }

    [Fact]
    public void Repeat_CountZero_ReturnsEmptyString() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.repeat", GrobValue.FromString("ab"), GrobValue.FromInt(0)));
        Assert.Equal(GrobValue.FromString(string.Empty), vm.Stack.Peek());
    }

    [Fact]
    public void Repeat_NegativeCount_ReturnsEmptyString() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.repeat", GrobValue.FromString("ab"), GrobValue.FromInt(-3)));
        Assert.Equal(GrobValue.FromString(string.Empty), vm.Stack.Peek());
    }

    [Fact]
    public void Repeat_CountExceedsAllocationCeiling_ThrowsCatchableIndexError() {
        // A count whose product with the receiver's length does not overflow the
        // checked(...) cast to int (so the VM's generic OverflowException handler never
        // gets a chance to help) but is still large enough to ask StringBuilder to
        // allocate an unreasonable buffer. The explicit ceiling must reject it as a
        // catchable GrobError before that allocation is attempted.
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.repeat", GrobValue.FromString("a"), GrobValue.FromInt(500_000_000))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Repeat_CountOverflowsCastEntirely_StillThrowsCatchableIndexError() {
        // A count large enough to overflow the checked(...) cast outright. Confirms the
        // new ceiling guard fires first and the fault is E5101/IndexError, not the
        // generic E5001/ArithmeticError the VM's OverflowException catch would otherwise
        // produce.
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.repeat", GrobValue.FromString("ab"), GrobValue.FromInt(long.MaxValue))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
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
    public void Left_NAtIntMaxValue_StillThrowsIndexErrorNotHostException() {
        // Locks in the audit finding: Left is already safe at int.MaxValue, bounded by
        // the explicit `n > s.Length` comparison rather than any arithmetic that could
        // wrap. A future change must not regress this to an unguarded cast/allocation.
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.left", GrobValue.FromString("hi"), GrobValue.FromInt(int.MaxValue))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Left_NegativeN_ThrowsIndexError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.left", GrobValue.FromString("hi"), GrobValue.FromInt(-1))));
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
    public void Right_NAtIntMaxValue_StillThrowsIndexErrorNotHostException() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.right", GrobValue.FromString("hi"), GrobValue.FromInt(int.MaxValue))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Right_NegativeN_ThrowsIndexError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.right", GrobValue.FromString("hi"), GrobValue.FromInt(-1))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void ToString_ReturnsReceiverUnchanged() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.toString", GrobValue.FromString("hi")));
        Assert.Equal(GrobValue.FromString("hi"), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // padLeft / padRight / truncate (D-365) — the runtime native is always
    // called at its full fixed arity; the compiler's default-argument-fill
    // synthesis is a compile-time concern tested at the Compiler layer, not
    // here. Pinned edge-case semantics: width/maxLength no larger than the
    // input (or negative) is a no-op; a multi-character pad char uses only
    // its first character; an empty pad char falls back to a space;
    // `maxLength` for truncate is the total result length including the
    // suffix, clamped to the suffix itself (or empty) when too small.
    // -----------------------------------------------------------------------

    [Fact]
    public void PadLeft_ShorterThanWidth_PadsWithDefaultSpace() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padLeft",
            GrobValue.FromString("7"), GrobValue.FromInt(3), GrobValue.FromString(" ")));
        Assert.Equal(GrobValue.FromString("  7"), vm.Stack.Peek());
    }

    [Fact]
    public void PadLeft_ShorterThanWidth_PadsWithSuppliedChar() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padLeft",
            GrobValue.FromString("7"), GrobValue.FromInt(3), GrobValue.FromString("0")));
        Assert.Equal(GrobValue.FromString("007"), vm.Stack.Peek());
    }

    [Theory]
    [InlineData("hello", 3)]
    [InlineData("hello", 5)]
    [InlineData("hello", -1)]
    public void PadLeft_WidthNoLargerThanInput_ReturnsInputUnchanged(string input, long width) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padLeft",
            GrobValue.FromString(input), GrobValue.FromInt(width), GrobValue.FromString(" ")));
        Assert.Equal(GrobValue.FromString(input), vm.Stack.Peek());
    }

    [Fact]
    public void PadLeft_MultiCharacterPadChar_UsesFirstCharacterOnly() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padLeft",
            GrobValue.FromString("7"), GrobValue.FromInt(3), GrobValue.FromString("xy")));
        Assert.Equal(GrobValue.FromString("xx7"), vm.Stack.Peek());
    }

    [Fact]
    public void PadLeft_EmptyPadChar_FallsBackToSpace() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padLeft",
            GrobValue.FromString("7"), GrobValue.FromInt(3), GrobValue.FromString("")));
        Assert.Equal(GrobValue.FromString("  7"), vm.Stack.Peek());
    }

    [Fact]
    public void PadRight_ShorterThanWidth_PadsWithDefaultSpace() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padRight",
            GrobValue.FromString("7"), GrobValue.FromInt(3), GrobValue.FromString(" ")));
        Assert.Equal(GrobValue.FromString("7  "), vm.Stack.Peek());
    }

    [Fact]
    public void PadRight_ShorterThanWidth_PadsWithSuppliedChar() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padRight",
            GrobValue.FromString("7"), GrobValue.FromInt(3), GrobValue.FromString("0")));
        Assert.Equal(GrobValue.FromString("700"), vm.Stack.Peek());
    }

    [Theory]
    [InlineData("hello", 3)]
    [InlineData("hello", 5)]
    [InlineData("hello", -1)]
    public void PadRight_WidthNoLargerThanInput_ReturnsInputUnchanged(string input, long width) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padRight",
            GrobValue.FromString(input), GrobValue.FromInt(width), GrobValue.FromString(" ")));
        Assert.Equal(GrobValue.FromString(input), vm.Stack.Peek());
    }

    [Fact]
    public void PadRight_MultiCharacterPadChar_UsesFirstCharacterOnly() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padRight",
            GrobValue.FromString("7"), GrobValue.FromInt(3), GrobValue.FromString("xy")));
        Assert.Equal(GrobValue.FromString("7xx"), vm.Stack.Peek());
    }

    [Fact]
    public void PadRight_EmptyPadChar_FallsBackToSpace() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.padRight",
            GrobValue.FromString("7"), GrobValue.FromInt(3), GrobValue.FromString("")));
        Assert.Equal(GrobValue.FromString("7  "), vm.Stack.Peek());
    }

    [Fact]
    public void PadLeft_WidthAboveIntMax_ThrowsIndexError() {
        // width is a 64-bit int; a value above int.MaxValue would wrap to a negative
        // on the unchecked cast to .NET's PadLeft overload and throw an uncoded CLR
        // fault that bypasses the native-throw seam. The guard rejects it as E5101 first.
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.padLeft",
                GrobValue.FromString("7"), GrobValue.FromInt((long)int.MaxValue + 1), GrobValue.FromString(" "))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void PadLeft_WidthExceedsAllocationCeiling_ThrowsIndexError() {
        // A width that fits comfortably in an int (nowhere near int.MaxValue, so the
        // cast-safety half of the guard would let it through) but is still large enough
        // that .NET's PadLeft would allocate an unreasonable buffer. The allocation
        // ceiling must reject it as a catchable GrobError.
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.padLeft",
                GrobValue.FromString("7"), GrobValue.FromInt(500_000_000), GrobValue.FromString(" "))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void PadRight_WidthAboveIntMax_ThrowsIndexError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.padRight",
                GrobValue.FromString("7"), GrobValue.FromInt((long)int.MaxValue + 1), GrobValue.FromString(" "))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void PadRight_WidthExceedsAllocationCeiling_ThrowsIndexError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("string.padRight",
                GrobValue.FromString("7"), GrobValue.FromInt(500_000_000), GrobValue.FromString(" "))));
        Assert.Equal(ErrorCatalog.E5101.Code, ex.Code);
    }

    [Fact]
    public void Truncate_LongerThanMaxLength_CutsToMaxLengthIncludingSuffix() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.truncate",
            GrobValue.FromString("hello world"), GrobValue.FromInt(8), GrobValue.FromString("...")));
        Assert.Equal(GrobValue.FromString("hello..."), vm.Stack.Peek());
    }

    [Fact]
    public void Truncate_LongerThanMaxLength_UsesSuppliedSuffix() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.truncate",
            GrobValue.FromString("hello world"), GrobValue.FromInt(8), GrobValue.FromString("-")));
        Assert.Equal(GrobValue.FromString("hello w-"), vm.Stack.Peek());
    }

    [Theory]
    [InlineData("hi", 10)]
    [InlineData("hi", 2)]
    public void Truncate_MaxLengthNoShorterThanInput_ReturnsInputUnchanged(string input, long maxLength) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.truncate",
            GrobValue.FromString(input), GrobValue.FromInt(maxLength), GrobValue.FromString("...")));
        Assert.Equal(GrobValue.FromString(input), vm.Stack.Peek());
    }

    [Fact]
    public void Truncate_MaxLengthShorterThanSuffix_ReturnsSuffixClampedToMaxLength() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.truncate",
            GrobValue.FromString("hello world"), GrobValue.FromInt(2), GrobValue.FromString("...")));
        Assert.Equal(GrobValue.FromString(".."), vm.Stack.Peek());
    }

    [Fact]
    public void Truncate_MaxLengthZero_ReturnsEmptyString() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.truncate",
            GrobValue.FromString("hello world"), GrobValue.FromInt(0), GrobValue.FromString("...")));
        Assert.Equal(GrobValue.FromString(""), vm.Stack.Peek());
    }

    [Fact]
    public void Truncate_NegativeMaxLength_ReturnsEmptyString() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("string.truncate",
            GrobValue.FromString("hello world"), GrobValue.FromInt(-1), GrobValue.FromString("...")));
        Assert.Equal(GrobValue.FromString(""), vm.Stack.Peek());
    }
}
