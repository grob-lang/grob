using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 9 Increment B — the <c>date</c> primitive's runtime
/// representation (a hidden-field <see cref="GrobStruct"/>, D-303's "boxed
/// <see cref="DateTimeOffset"/>" reconciled against the unconditional <c>Struct</c>-only
/// payload), value equality (delegates to the unmodified <see cref="GrobStruct.Equals"/>),
/// the new <see cref="OpCode.GetProperty"/> arm for the property/method surface, and the
/// new <see cref="OpCode.LessDate"/>/<see cref="OpCode.GreaterDate"/> opcodes (D-354). All
/// chunks are hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineDateTests {
    private static readonly DateTimeOffset SampleInstant =
        new(2026, 4, 5, 14, 30, 0, TimeSpan.FromHours(1));
    private const string SampleIso = "2026-04-05T14:30:00+01:00";

    private static (VirtualMachine Vm, StringWriter Output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static GrobValue Date(DateTimeOffset value) => DateNatives.FromDateTimeOffset(value);

    // -----------------------------------------------------------------------
    // Value equality — delegates to GrobStruct.Equals; no VM change needed.
    // -----------------------------------------------------------------------

    [Fact]
    public void Equal_TwoDatesSameValue_IndependentlyConstructed_IsTrue() {
        var chunk = new Chunk();
        byte a = (byte)chunk.AddConstant(Date(SampleInstant));
        byte b = (byte)chunk.AddConstant(Date(SampleInstant));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(a, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(b, 1);
        chunk.WriteOpCode(OpCode.Equal, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Equal_DifferentDates_IsFalse() {
        var chunk = new Chunk();
        byte a = (byte)chunk.AddConstant(Date(SampleInstant));
        byte b = (byte)chunk.AddConstant(Date(SampleInstant.AddDays(1)));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(a, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(b, 1);
        chunk.WriteOpCode(OpCode.Equal, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // LessDate/GreaterDate (D-354) — compares the underlying instant.
    // -----------------------------------------------------------------------

    [Fact]
    public void LessDate_EarlierThanLater_IsTrue() {
        var chunk = new Chunk();
        byte a = (byte)chunk.AddConstant(Date(SampleInstant));
        byte b = (byte)chunk.AddConstant(Date(SampleInstant.AddDays(1)));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(a, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(b, 1);
        chunk.WriteOpCode(OpCode.LessDate, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void GreaterDate_LaterThanEarlier_IsTrue() {
        var chunk = new Chunk();
        byte a = (byte)chunk.AddConstant(Date(SampleInstant.AddDays(1)));
        byte b = (byte)chunk.AddConstant(Date(SampleInstant));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(a, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(b, 1);
        chunk.WriteOpCode(OpCode.GreaterDate, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void LessDate_ComparesAcrossDifferingOffsets_ByInstant() {
        // Same wall-clock hour, but +01:00 is one hour earlier in absolute time than
        // +00:00 at the same clock reading — DateTimeOffset's own operator< normalises
        // this; a naive field-by-field compare would get it backwards.
        var earlier = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(1)); // 11:00 UTC
        var later = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);           // 12:00 UTC

        var chunk = new Chunk();
        byte a = (byte)chunk.AddConstant(Date(earlier));
        byte b = (byte)chunk.AddConstant(Date(later));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(a, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(b, 1);
        chunk.WriteOpCode(OpCode.LessDate, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // GetProperty — direct-value properties.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("year", 2026)]
    [InlineData("month", 4)]
    [InlineData("day", 5)]
    [InlineData("hour", 14)]
    [InlineData("minute", 30)]
    [InlineData("second", 0)]
    public void GetProperty_IntComponent_ReadsFromStoredInstant(string property, long expected) {
        var chunk = new Chunk();
        byte d = (byte)chunk.AddConstant(Date(SampleInstant));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString(property));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(d, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(expected, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void GetProperty_UtcOffset_ReadsMinutes() {
        var chunk = new Chunk();
        byte d = (byte)chunk.AddConstant(Date(SampleInstant));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString("utcOffset"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(d, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(60, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void GetProperty_DayOfWeek_ReadsName() {
        var chunk = new Chunk();
        byte d = (byte)chunk.AddConstant(Date(SampleInstant)); // 2026-04-05 is a Sunday
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString("dayOfWeek"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(d, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal("Sunday", vm.Stack.Peek().AsString());
    }

    // -----------------------------------------------------------------------
    // GetProperty — bound-method call (mirrors the guid precedent), with real arguments.
    // -----------------------------------------------------------------------

    [Fact]
    public void GetProperty_AddDays_BoundMethodCall_AdvancesDate() {
        var chunk = new Chunk();
        byte d = (byte)chunk.AddConstant(Date(SampleInstant));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString("addDays"));
        byte argIdx = (byte)chunk.AddConstant(GrobValue.FromInt(1));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(d, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(argIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(1, 1);
        byte dayIdx = (byte)chunk.AddConstant(GrobValue.FromString("day"));
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(dayIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(6, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void GetProperty_AddDays_NegativeArgument_MovesBackward() {
        var chunk = new Chunk();
        byte d = (byte)chunk.AddConstant(Date(SampleInstant));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString("addDays"));
        byte argIdx = (byte)chunk.AddConstant(GrobValue.FromInt(-1));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(d, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(argIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(1, 1);
        byte dayIdx = (byte)chunk.AddConstant(GrobValue.FromString("day"));
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(dayIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(4, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // ValueDisplay integration (D-336) — a registered toString() renders the canonical
    // ISO-8601 string through print(), never the hidden-field structural form.
    // -----------------------------------------------------------------------

    [Fact]
    public void Print_RegisteredToString_RendersCanonicalIsoString_NotHiddenField() {
        var chunk = new Chunk();
        byte d = (byte)chunk.AddConstant(Date(SampleInstant));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(d, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, output) = NewVm();
        vm.RegisterToString(DateNatives.TypeName, v => DateNatives.IsoDateTimeString(v.AsStruct()));
        vm.Run(chunk);

        string printed = output.ToString();
        Assert.Equal($"{SampleIso}{Environment.NewLine}", printed);
        Assert.DoesNotContain(DateNatives.ValueFieldName, printed);
        Assert.DoesNotContain("[date]", printed);
    }

    [Fact]
    public void Print_NoRegisteredToString_FallsThroughToStructuralRendering() {
        var chunk = new Chunk();
        byte d = (byte)chunk.AddConstant(Date(SampleInstant));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(d, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Contains(DateNatives.ValueFieldName, output.ToString());
    }

    [Fact]
    public void GetProperty_UnknownMember_ThrowsInternalException() {
        // Defensive branch: the type checker rejects an unknown date member before
        // emission (TypeCheckerDateTests.UnknownMethod_Call_ReportsSingleE1002), so this
        // is only reachable via hand-built bytecode that bypasses the checker entirely.
        var chunk = new Chunk();
        byte d = (byte)chunk.AddConstant(Date(SampleInstant));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString("nope"));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(d, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();

        Assert.Throws<GrobInternalException>(() => vm.Run(chunk));
    }
}
