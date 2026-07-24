using Grob.Core;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 9 Increment A1b (D-370) — <see cref="NumericStaticsPlugin"/> registers the
/// <c>int</c>/<c>float</c> type-static namespace natives (<c>min</c>/<c>max</c>/<c>clamp</c>),
/// the compile-time twin of <c>NamespaceRegistry</c>'s <c>int</c>/<c>float</c> entries
/// (<c>Grob.Compiler</c>) rather than <c>PrimitiveMemberRegistry</c> — a separate registry
/// lineage from D-369's <see cref="NumericMethodsPlugin"/> (instance methods), even though
/// both plugins register natives under the same <c>"int."</c>/<c>"float."</c> qualified-name
/// prefix. Each native takes its arguments positionally, no receiver injection (this is a
/// namespace-receiver call, not an instance method) — mirrors <c>MathPluginTests</c>'
/// <see cref="ChunkBuilders.BuildCallChunk"/> shape.
/// </summary>
public sealed class NumericStaticsPluginTests {
    private static VirtualMachine NewRegisteredVm() {
        var vm = new VirtualMachine(new StringWriter());
        new NumericStaticsPlugin().Register(vm);
        return vm;
    }

    [Fact]
    public void Name_IsNumericStatics() {
        Assert.Equal("numericStatics", new NumericStaticsPlugin().Name);
    }

    [Fact]
    public void Register_AddsExactlyTheSixQualifiedNatives() {
        var vm = new VirtualMachine(new StringWriter());
        new NumericStaticsPlugin().Register(vm);

        string[] expected = [
            "int.min", "int.max", "int.clamp",
            "float.min", "float.max", "float.clamp",
        ];
        foreach (string name in expected) {
            Assert.True(vm.Globals.ContainsKey(name), $"missing native registration: {name}");
        }
        Assert.Equal(6, vm.Globals.Count);
    }

    // -----------------------------------------------------------------------
    // int.min / int.max
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(3L, 5L, 3L)]
    [InlineData(5L, 3L, 3L)]
    [InlineData(-5L, -3L, -5L)]
    [InlineData(4L, 4L, 4L)]
    public void IntMin_ReturnsSmaller(long a, long b, long expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("int.min", GrobValue.FromInt(a), GrobValue.FromInt(b)));
        Assert.Equal(GrobValue.FromInt(expected), vm.Stack.Peek());
    }

    [Theory]
    [InlineData(3L, 5L, 5L)]
    [InlineData(5L, 3L, 5L)]
    [InlineData(-5L, -3L, -3L)]
    [InlineData(4L, 4L, 4L)]
    public void IntMax_ReturnsLarger(long a, long b, long expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("int.max", GrobValue.FromInt(a), GrobValue.FromInt(b)));
        Assert.Equal(GrobValue.FromInt(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // int.clamp — at and outside both bounds.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(150L, 0L, 100L, 100L)] // above hi -> clamps to hi
    [InlineData(-10L, 0L, 100L, 0L)]   // below lo -> clamps to lo
    [InlineData(50L, 0L, 100L, 50L)]   // inside range -> unchanged
    [InlineData(0L, 0L, 100L, 0L)]     // at lo -> unchanged
    [InlineData(100L, 0L, 100L, 100L)] // at hi -> unchanged
    public void IntClamp_ClampsToRange(long v, long lo, long hi, long expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("int.clamp", GrobValue.FromInt(v), GrobValue.FromInt(lo), GrobValue.FromInt(hi)));
        Assert.Equal(GrobValue.FromInt(expected), vm.Stack.Peek());
    }

    [Fact]
    public void IntClamp_LoGreaterThanHi_ThrowsCatchableArithmeticError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("int.clamp", GrobValue.FromInt(5), GrobValue.FromInt(10), GrobValue.FromInt(0))));
        Assert.Equal(ErrorCatalog.E5001.Code, ex.Code);
    }

    // -----------------------------------------------------------------------
    // float.min / float.max
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(3.5, 5.5, 3.5)]
    [InlineData(5.5, 3.5, 3.5)]
    [InlineData(-5.5, -3.5, -5.5)]
    public void FloatMin_ReturnsSmaller(double a, double b, double expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.min", GrobValue.FromFloat(a), GrobValue.FromFloat(b)));
        Assert.Equal(GrobValue.FromFloat(expected), vm.Stack.Peek());
    }

    [Theory]
    [InlineData(3.5, 5.5, 5.5)]
    [InlineData(5.5, 3.5, 5.5)]
    [InlineData(-5.5, -3.5, -3.5)]
    public void FloatMax_ReturnsLarger(double a, double b, double expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.max", GrobValue.FromFloat(a), GrobValue.FromFloat(b)));
        Assert.Equal(GrobValue.FromFloat(expected), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // float.min/max with NaN — .NET's Math.Min/Math.Max(double, double) return NaN
    // if either argument is NaN. Pinned, not special-cased. Both argument positions.
    // -----------------------------------------------------------------------

    [Fact]
    public void FloatMin_NaNAsFirstArgument_ReturnsNaN() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.min", GrobValue.FromFloat(double.NaN), GrobValue.FromFloat(1.0)));
        Assert.True(double.IsNaN(vm.Stack.Peek().AsFloat()));
    }

    [Fact]
    public void FloatMin_NaNAsSecondArgument_ReturnsNaN() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.min", GrobValue.FromFloat(1.0), GrobValue.FromFloat(double.NaN)));
        Assert.True(double.IsNaN(vm.Stack.Peek().AsFloat()));
    }

    [Fact]
    public void FloatMax_NaNAsFirstArgument_ReturnsNaN() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.max", GrobValue.FromFloat(double.NaN), GrobValue.FromFloat(1.0)));
        Assert.True(double.IsNaN(vm.Stack.Peek().AsFloat()));
    }

    [Fact]
    public void FloatMax_NaNAsSecondArgument_ReturnsNaN() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.max", GrobValue.FromFloat(1.0), GrobValue.FromFloat(double.NaN)));
        Assert.True(double.IsNaN(vm.Stack.Peek().AsFloat()));
    }

    // -----------------------------------------------------------------------
    // float.min/max with +0.0/-0.0 — .NET treats -0.0 as less than +0.0 here
    // (consistent with D-315's float-equality semantics). Both orders.
    // -----------------------------------------------------------------------

    [Fact]
    public void FloatMin_PositiveThenNegativeZero_ReturnsNegativeZero() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.min", GrobValue.FromFloat(0.0), GrobValue.FromFloat(-0.0)));
        double result = vm.Stack.Peek().AsFloat();
        Assert.Equal(0.0, result);
        Assert.True(double.IsNegative(result));
    }

    [Fact]
    public void FloatMin_NegativeThenPositiveZero_ReturnsNegativeZero() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.min", GrobValue.FromFloat(-0.0), GrobValue.FromFloat(0.0)));
        double result = vm.Stack.Peek().AsFloat();
        Assert.Equal(0.0, result);
        Assert.True(double.IsNegative(result));
    }

    [Fact]
    public void FloatMax_PositiveThenNegativeZero_ReturnsPositiveZero() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.max", GrobValue.FromFloat(0.0), GrobValue.FromFloat(-0.0)));
        double result = vm.Stack.Peek().AsFloat();
        Assert.Equal(0.0, result);
        Assert.False(double.IsNegative(result));
    }

    [Fact]
    public void FloatMax_NegativeThenPositiveZero_ReturnsPositiveZero() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.max", GrobValue.FromFloat(-0.0), GrobValue.FromFloat(0.0)));
        double result = vm.Stack.Peek().AsFloat();
        Assert.Equal(0.0, result);
        Assert.False(double.IsNegative(result));
    }

    // -----------------------------------------------------------------------
    // float.clamp — at and outside both bounds, plus the lo > hi fault.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(1.5, 0.0, 1.0, 1.0)]  // above hi -> clamps to hi
    [InlineData(-0.5, 0.0, 1.0, 0.0)] // below lo -> clamps to lo
    [InlineData(0.5, 0.0, 1.0, 0.5)]  // inside range -> unchanged
    public void FloatClamp_ClampsToRange(double v, double lo, double hi, double expected) {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("float.clamp",
            GrobValue.FromFloat(v), GrobValue.FromFloat(lo), GrobValue.FromFloat(hi)));
        Assert.Equal(GrobValue.FromFloat(expected), vm.Stack.Peek());
    }

    [Fact]
    public void FloatClamp_LoGreaterThanHi_ThrowsCatchableArithmeticError() {
        var vm = NewRegisteredVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("float.clamp",
                GrobValue.FromFloat(0.5), GrobValue.FromFloat(1.0), GrobValue.FromFloat(0.0))));
        Assert.Equal(ErrorCatalog.E5001.Code, ex.Code);
    }
}
