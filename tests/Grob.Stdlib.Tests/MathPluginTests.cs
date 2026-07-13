using Grob.Core;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 8 Increment A's proving vertical: <see cref="MathPlugin"/> registers exactly
/// <c>math.pi</c> (a namespace constant) and <c>math.sqrt</c> (a native that throws
/// <c>ArithmeticError</c> on a domain violation) via <see cref="IGrobPlugin"/>, end to
/// end through a real <see cref="VirtualMachine"/>. Chunks are hand-constructed — this
/// project has no dependency on <c>Grob.Compiler</c>.
/// </summary>
public sealed class MathPluginTests {
    private static VirtualMachine NewRegisteredVm(Grob.Runtime.IRandomSource? randomSource = null) {
        var vm = new VirtualMachine(new StringWriter());
        new MathPlugin(randomSource ?? new TestRandomSource(12345)).Register(vm);
        return vm;
    }

    [Fact]
    public void MathPi_IsPiToFullDoublePrecision() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetGlobalChunk("math.pi"));

        Assert.Equal(GrobValue.FromFloat(3.141592653589793), vm.Stack.Peek());
    }

    [Fact]
    public void MathSqrt_OfNine_ReturnsThree() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.sqrt", GrobValue.FromFloat(9.0)));

        Assert.Equal(GrobValue.FromFloat(3.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathSqrt_OfZero_ReturnsZero() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.sqrt", GrobValue.FromFloat(0.0)));

        Assert.Equal(GrobValue.FromFloat(0.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathSqrt_OfNegative_ThrowsArithmeticDomainFault() {
        var vm = NewRegisteredVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("math.sqrt", GrobValue.FromFloat(-1.0))));

        Assert.Equal(ErrorCatalog.E5006.Code, ex.Code);
        Assert.Contains("-1", ex.Message);
    }

    [Fact]
    public void MathSqrt_NegativeInsideTryCatch_IsCatchableArithmeticError() {
        // Proves the native-throw seam end to end through the registered plugin:
        // the domain fault unwinds through the SAME handler-table walk a user throw
        // uses, not a bespoke path.
        var script = new Chunk();
        int calleeIdx = script.AddConstant(GrobValue.FromString("math.sqrt"));
        int regionIndex = script.AddTryRegion();

        script.WriteOpCode(OpCode.TryBegin, 1); script.WriteByte((byte)regionIndex, 1);
        int startOffset = script.Count;

        script.WriteOpCode(OpCode.GetGlobal, 2); script.WriteByte((byte)calleeIdx, 2);
        int argIdx = script.AddConstant(GrobValue.FromFloat(-4.0));
        script.WriteOpCode(OpCode.Constant, 2); script.WriteByte((byte)argIdx, 2);
        script.WriteOpCode(OpCode.Call, 2); script.WriteByte(1, 2);
        script.WriteOpCode(OpCode.Pop, 2);
        int endOffset = script.Count;

        script.WriteOpCode(OpCode.Jump, 2);
        int jumpSite = script.Count;
        script.WriteByte(0xFF, 2); script.WriteByte(0xFF, 2);

        int handlerOffset = script.Count; // empty catch — binds at slot 0

        int offset = script.Count - (jumpSite + 2);
        script.PatchByte(jumpSite, (byte)(offset >> 8));
        script.PatchByte(jumpSite + 1, (byte)(offset & 0xFF));

        script.WriteOpCode(OpCode.TryEnd, 3);
        script.WriteOpCode(OpCode.Return, 3);

        script.SetTryRegion(regionIndex, new TryRegion(startOffset, endOffset,
            [new CatchHandler(["ArithmeticError"], IsCatchAll: false, handlerOffset, BindingSlot: 0)]));

        var vm = NewRegisteredVm();
        vm.Run(script);

        GrobValue bound = vm.Stack.GetSlot(0);
        Assert.True(bound.TryAsStruct(out GrobStruct? s));
        Assert.Equal("ArithmeticError", s!.TypeName);
    }

    [Fact]
    public void Register_AddsExactlyTheDocumentedMathMembers() {
        var vm = new VirtualMachine(new StringWriter());
        new MathPlugin(new TestRandomSource(1)).Register(vm);

        string[] expectedMembers = [
            "pi", "e", "tau",
            "sqrt", "pow", "log", "log10", "sin", "cos", "tan",
            "asin", "acos", "atan", "atan2", "toRadians", "toDegrees",
            "random", "randomInt", "randomSeed",
        ];
        foreach (string member in expectedMembers) {
            Assert.True(vm.Globals.ContainsKey($"math.{member}"), $"missing math.{member}");
        }
        Assert.Equal(expectedMembers.Length, vm.Globals.Count);
    }

    [Fact]
    public void Name_IsMath() {
        Assert.Equal("math", new MathPlugin(new TestRandomSource(1)).Name);
    }

    // -----------------------------------------------------------------------
    // Sprint 8 Increment B — constants, pow/log/trig, degrees/radians.
    // -----------------------------------------------------------------------

    [Fact]
    public void MathE_IsEulersNumber() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetGlobalChunk("math.e"));

        Assert.Equal(GrobValue.FromFloat(Math.E), vm.Stack.Peek());
    }

    [Fact]
    public void MathTau_IsTwoPi() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetGlobalChunk("math.tau"));

        Assert.Equal(GrobValue.FromFloat(2.0 * Math.PI), vm.Stack.Peek());
    }

    [Fact]
    public void MathPow_TwoToTen_ReturnsOneThousandTwentyFour() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.pow", GrobValue.FromFloat(2.0), GrobValue.FromFloat(10.0)));

        Assert.Equal(GrobValue.FromFloat(1024.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathPow_ZeroToNegativeOne_ReturnsPositiveInfinity_DoesNotThrow() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.pow", GrobValue.FromFloat(0.0), GrobValue.FromFloat(-1.0)));

        Assert.Equal(double.PositiveInfinity, vm.Stack.Peek().AsFloat());
    }

    [Fact]
    public void MathPow_NegativeBaseFractionalExponent_ReturnsNaN_DoesNotThrow() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.pow", GrobValue.FromFloat(-2.0), GrobValue.FromFloat(0.5)));

        Assert.True(double.IsNaN(vm.Stack.Peek().AsFloat()));
    }

    [Fact]
    public void MathLog_OfE_ReturnsOne() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.log", GrobValue.FromFloat(Math.E)));

        Assert.Equal(GrobValue.FromFloat(1.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathLog_OfZero_ThrowsArithmeticDomainFault() {
        var vm = NewRegisteredVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("math.log", GrobValue.FromFloat(0.0))));

        Assert.Equal(ErrorCatalog.E5006.Code, ex.Code);
    }

    [Fact]
    public void MathLog10_OfOneHundred_ReturnsTwo() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.log10", GrobValue.FromFloat(100.0)));

        Assert.Equal(GrobValue.FromFloat(2.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathLog10_OfNegative_ThrowsArithmeticDomainFault() {
        var vm = NewRegisteredVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("math.log10", GrobValue.FromFloat(-1.0))));

        Assert.Equal(ErrorCatalog.E5006.Code, ex.Code);
    }

    [Fact]
    public void MathSin_OfHalfPi_ReturnsOne() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.sin", GrobValue.FromFloat(Math.PI / 2.0)));

        Assert.Equal(GrobValue.FromFloat(1.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathCos_OfZero_ReturnsOne() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.cos", GrobValue.FromFloat(0.0)));

        Assert.Equal(GrobValue.FromFloat(1.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathTan_OfQuarterPi_IsApproximatelyOne() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.tan", GrobValue.FromFloat(Math.PI / 4.0)));

        Assert.Equal(1.0, vm.Stack.Peek().AsFloat(), precision: 10);
    }

    [Fact]
    public void MathAsin_OfOne_IsApproximatelyHalfPi() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.asin", GrobValue.FromFloat(1.0)));

        Assert.Equal(Math.PI / 2.0, vm.Stack.Peek().AsFloat(), precision: 10);
    }

    [Fact]
    public void MathAsin_AboveOne_ThrowsArithmeticDomainFault() {
        var vm = NewRegisteredVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("math.asin", GrobValue.FromFloat(2.0))));

        Assert.Equal(ErrorCatalog.E5006.Code, ex.Code);
    }

    [Fact]
    public void MathAcos_OfOne_ReturnsZero() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.acos", GrobValue.FromFloat(1.0)));

        Assert.Equal(GrobValue.FromFloat(0.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathAcos_BelowNegativeOne_ThrowsArithmeticDomainFault() {
        var vm = NewRegisteredVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("math.acos", GrobValue.FromFloat(-2.0))));

        Assert.Equal(ErrorCatalog.E5006.Code, ex.Code);
    }

    [Fact]
    public void MathAtan_OfOne_IsApproximatelyQuarterPi() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.atan", GrobValue.FromFloat(1.0)));

        Assert.Equal(Math.PI / 4.0, vm.Stack.Peek().AsFloat(), precision: 10);
    }

    [Fact]
    public void MathAtan2_OfZeroZero_ReturnsZero_DoesNotThrow() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.atan2", GrobValue.FromFloat(0.0), GrobValue.FromFloat(0.0)));

        Assert.Equal(GrobValue.FromFloat(0.0), vm.Stack.Peek());
    }

    [Fact]
    public void MathToRadians_OneEighty_IsApproximatelyPi() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.toRadians", GrobValue.FromFloat(180.0)));

        Assert.Equal(Math.PI, vm.Stack.Peek().AsFloat(), precision: 10);
    }

    [Fact]
    public void MathToDegrees_OfPi_ReturnsOneEighty() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.toDegrees", GrobValue.FromFloat(Math.PI)));

        Assert.Equal(GrobValue.FromFloat(180.0), vm.Stack.Peek());
    }

    // -----------------------------------------------------------------------
    // Sprint 8 Increment B — random/randomInt/randomSeed on IRandomSource.
    // -----------------------------------------------------------------------

    [Fact]
    public void MathRandomSeed_SameSeedTwice_ProducesReproducibleSequence() {
        var vm1 = NewRegisteredVm(new TestRandomSource(0));
        vm1.Run(BuildCallChunk("math.randomSeed", GrobValue.FromInt(42)));
        vm1.Run(BuildCallChunk("math.random"));
        double first1 = vm1.Stack.Peek().AsFloat();
        vm1.Run(BuildCallChunk("math.randomInt", GrobValue.FromInt(1), GrobValue.FromInt(6)));
        long second1 = vm1.Stack.Peek().AsInt();

        var vm2 = NewRegisteredVm(new TestRandomSource(0));
        vm2.Run(BuildCallChunk("math.randomSeed", GrobValue.FromInt(42)));
        vm2.Run(BuildCallChunk("math.random"));
        double first2 = vm2.Stack.Peek().AsFloat();
        vm2.Run(BuildCallChunk("math.randomInt", GrobValue.FromInt(1), GrobValue.FromInt(6)));
        long second2 = vm2.Stack.Peek().AsInt();

        Assert.Equal(first1, first2);
        Assert.Equal(second1, second2);
    }

    [Fact]
    public void MathRandom_ReturnsValueInZeroToOneExclusiveUpper() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.random"));

        double value = vm.Stack.Peek().AsFloat();
        Assert.True(value >= 0.0 && value < 1.0, $"out of range: {value}");
    }

    [Fact]
    public void MathRandomInt_OverManyDraws_StaysWithinInclusiveBoundsAndReachesBothEnds() {
        var vm = NewRegisteredVm(new TestRandomSource(7));
        bool sawMin = false;
        bool sawMax = false;

        for (int i = 0; i < 500; i++) {
            vm.Run(BuildCallChunk("math.randomInt", GrobValue.FromInt(1), GrobValue.FromInt(6)));
            long value = vm.Stack.Peek().AsInt();
            Assert.True(value is >= 1 and <= 6, $"out of range: {value}");
            if (value == 1) sawMin = true;
            if (value == 6) sawMax = true;
        }

        Assert.True(sawMin, "never drew the minimum (1) over 500 draws");
        Assert.True(sawMax, "never drew the maximum (6) over 500 draws");
    }

    [Fact]
    public void MathRandomSeed_ReturnsNil() {
        var vm = NewRegisteredVm();
        vm.Run(BuildCallChunk("math.randomSeed", GrobValue.FromInt(1)));

        Assert.True(vm.Stack.Peek().IsNil);
    }
}
