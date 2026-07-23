using Grob.Core;
using Grob.Core.PrimitiveMembers;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 9 Increment A1a (D-369) — <see cref="NumericMethodsPlugin"/> registers the
/// <c>int</c>/<c>float</c>/<c>bool</c> instance surfaces' runtime natives (D-066's
/// compile-time-sugar model, proven on <c>string</c> by D-363; <c>PrimitiveMemberRegistry</c>
/// is the compile-time twin). Every native takes the receiver as its first argument (the
/// compiler injects it), so a hand-built <see cref="ChunkBuilders.BuildCallChunk"/> call with
/// the receiver as <c>args[0]</c> exercises the exact shape the compiler emits. End to end
/// through a real <see cref="VirtualMachine"/>, mirroring <c>StringMethodsPluginTests</c>.
/// </summary>
public sealed class NumericMethodsPluginTests {
    private static VirtualMachine NewRegisteredVm() {
        var vm = new VirtualMachine(new StringWriter());
        new NumericMethodsPlugin().Register(vm);
        return vm;
    }

    [Fact]
    public void Name_IsNumeric() {
        Assert.Equal("numeric", new NumericMethodsPlugin().Name);
    }

    [Fact]
    public void Register_AddsExactlyTheThirteenQualifiedNatives() {
        var vm = new VirtualMachine(new StringWriter());
        new NumericMethodsPlugin().Register(vm);

        IEnumerable<string> expected = PrimitiveMemberRegistry.Int.Methods.Values.Select(m => m.QualifiedNativeName)
            .Concat(PrimitiveMemberRegistry.Float.Methods.Values.Select(m => m.QualifiedNativeName))
            .Concat(PrimitiveMemberRegistry.Bool.Methods.Values.Select(m => m.QualifiedNativeName));
        foreach (string name in expected) {
            Assert.True(vm.Globals.ContainsKey(name), $"missing native registration: {name}");
        }
        Assert.Equal(13, vm.Globals.Count);
    }

    // -----------------------------------------------------------------------
    // int.toString / int.toFloat
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(42L, "42")]
    [InlineData(-7L, "-7")]
    [InlineData(0L, "0")]
    public void IntToString_UsesInvariantCultureDecimalForm(long value, string expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("int.toString", GrobValue.FromInt(value)));
        Assert.Equal(GrobValue.FromString(expected), vm.Stack.Peek());
    }

    [Theory]
    [InlineData(3L, 3.0)]
    [InlineData(-3L, -3.0)]
    [InlineData(0L, 0.0)]
    public void IntToFloat_WidensExactly(long value, double expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("int.toFloat", GrobValue.FromInt(value)));
        Assert.Equal(GrobValue.FromFloat(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // int.abs — checked(...) cast/negation (Pattern A): overflow surfaces as the VM's
    // existing E5001/ArithmeticError via the outer OverflowException catch, not a
    // manual NativeFaultException guard.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(5L, 5L)]
    [InlineData(-5L, 5L)]
    [InlineData(0L, 0L)]
    public void IntAbs_ReturnsMagnitude(long value, long expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("int.abs", GrobValue.FromInt(value)));
        Assert.Equal(GrobValue.FromInt(expected), vm.Stack.Peek());
    }

    [Fact]
    public void IntAbs_OnLongMinValue_ThrowsCatchableArithmeticError() {
        // -long.MinValue is not representable — the checked(...) negation overflows,
        // caught by the VM's existing outer OverflowException handler (Pattern A),
        // exactly as NegateInt's own checked(-a) already does.
        var vm = NewRegisteredVm();
        GrobArithmeticException ex = Assert.Throws<GrobArithmeticException>(() =>
            vm.Run(BuildCallChunk("int.abs", GrobValue.FromInt(long.MinValue))));
        Assert.Equal(ErrorCatalog.E5001.Code, ex.Code);
    }

    // -----------------------------------------------------------------------
    // int.format — routes through long.ToString(pattern, InvariantCulture).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(1234L, "N0", "1,234")]
    [InlineData(255L, "X8", "000000FF")]
    [InlineData(42L, "D5", "00042")]
    public void IntFormat_RoutesThroughInvariantCultureToString(long value, string pattern, string expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("int.format", GrobValue.FromInt(value), GrobValue.FromString(pattern)));
        Assert.Equal(GrobValue.FromString(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // float.toString — must reproduce ValueDisplay.FormatFloat exactly (print() parity).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(3.5, "3.5")]
    [InlineData(3.0, "3.0")]
    [InlineData(-2.0, "-2.0")]
    [InlineData(0.0, "0.0")]
    public void FloatToString_MatchesValueDisplayFormatFloat(double value, string expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.toString", GrobValue.FromFloat(value)));
        Assert.Equal(GrobValue.FromString(expected), vm.Stack.Peek());
    }

    [Theory]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    public void FloatToString_PinnedNonFiniteSpellings(double value, string expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.toString", GrobValue.FromFloat(value)));
        Assert.Equal(GrobValue.FromString(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // float.toInt — truncates; faults out of range/NaN/Infinity via checked(...) cast
    // (Pattern A), the VM's existing OverflowException handler converting to E5001.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(3.9, 3L)]
    [InlineData(-3.9, -3L)]
    [InlineData(3.1, 3L)]
    [InlineData(0.0, 0L)]
    public void FloatToInt_Truncates(double value, long expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.toInt", GrobValue.FromFloat(value)));
        Assert.Equal(GrobValue.FromInt(expected), vm.Stack.Peek());
    }

    [Theory]
    [InlineData(1e300)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FloatToInt_OutOfRangeOrNonFinite_ThrowsCatchableArithmeticError(double value) {
        var vm = NewRegisteredVm();
        GrobArithmeticException ex = Assert.Throws<GrobArithmeticException>(() =>
            vm.Run(BuildCallChunk("float.toInt", GrobValue.FromFloat(value))));
        Assert.Equal(ErrorCatalog.E5001.Code, ex.Code);
    }

    // -----------------------------------------------------------------------
    // float.round — nearest integer, MidpointRounding.AwayFromZero (D-369 pinned rule,
    // no prior codebase precedent).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(2.5, 3L)]
    [InlineData(-2.5, -3L)]
    [InlineData(2.4, 2L)]
    [InlineData(2.6, 3L)]
    [InlineData(-2.4, -2L)]
    [InlineData(-2.6, -3L)]
    [InlineData(0.5, 1L)]
    [InlineData(-0.5, -1L)]
    public void FloatRound_UsesAwayFromZeroMidpointRule(double value, long expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.round", GrobValue.FromFloat(value)));
        Assert.Equal(GrobValue.FromInt(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // float.roundTo(decimals) — N decimal places, same AwayFromZero rule; decimals
    // outside .NET's Math.Round(double,int,MidpointRounding) supported [0,15] range
    // faults catchably (D-369: closes the ArgumentOutOfRangeException gap that pattern
    // would otherwise leave as an uncaught host exception, reusing E5001/ArithmeticError
    // rather than minting a new code).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(3.14159, 2, 3.14)]
    [InlineData(2.5, 0, 3.0)]
    [InlineData(-2.5, 0, -3.0)]
    [InlineData(1.0, 15, 1.0)]
    [InlineData(0.125, 2, 0.13)]
    public void FloatRoundTo_RoundsToDecimalPlaces(double value, long decimals, double expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.roundTo", GrobValue.FromFloat(value), GrobValue.FromInt(decimals)));
        Assert.Equal(GrobValue.FromFloat(expected), vm.Stack.Peek());
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(16L)]
    public void FloatRoundTo_DecimalsOutOfSupportedRange_ThrowsCatchableArithmeticError(long decimals) {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("float.roundTo", GrobValue.FromFloat(1.5), GrobValue.FromInt(decimals))));
        Assert.Equal(ErrorCatalog.E5001.Code, ex.Code);
    }

    // -----------------------------------------------------------------------
    // float.floor / float.ceil
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(2.9, 2L)]
    [InlineData(-2.1, -3L)]
    [InlineData(2.0, 2L)]
    public void FloatFloor_RoundsTowardNegativeInfinity(double value, long expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.floor", GrobValue.FromFloat(value)));
        Assert.Equal(GrobValue.FromInt(expected), vm.Stack.Peek());
    }

    [Theory]
    [InlineData(2.1, 3L)]
    [InlineData(-2.9, -2L)]
    [InlineData(2.0, 2L)]
    public void FloatCeil_RoundsTowardPositiveInfinity(double value, long expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.ceil", GrobValue.FromFloat(value)));
        Assert.Equal(GrobValue.FromInt(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // float.abs
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(-3.5, 3.5)]
    [InlineData(3.5, 3.5)]
    [InlineData(0.0, 0.0)]
    public void FloatAbs_ReturnsMagnitude(double value, double expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.abs", GrobValue.FromFloat(value)));
        Assert.Equal(GrobValue.FromFloat(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // float.format — routes through double.ToString(pattern, InvariantCulture).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(1234.5, "N2", "1,234.50")]
    [InlineData(0.125, "P1", "12.5 %")]
    [InlineData(3.14159, "F2", "3.14")]
    public void FloatFormat_RoutesThroughInvariantCultureToString(double value, string pattern, string expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.format", GrobValue.FromFloat(value), GrobValue.FromString(pattern)));
        Assert.Equal(GrobValue.FromString(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // bool.toString
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void BoolToString_ReturnsLowerCaseSpelling(bool value, string expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("bool.toString", GrobValue.FromBool(value)));
        Assert.Equal(GrobValue.FromString(expected), vm.Stack.Peek());
    }
}
