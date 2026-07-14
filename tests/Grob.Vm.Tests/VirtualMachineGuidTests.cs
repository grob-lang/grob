using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch tests for Sprint 8 Increment D — the <c>guid</c> primitive's runtime
/// representation (a hidden-field <see cref="GrobStruct"/>, D-303's "boxed
/// <see cref="Guid"/>" reconciled against the unconditional <c>Struct</c>-only payload),
/// value equality (delegates to the unmodified <see cref="GrobStruct.Equals"/>), and the
/// new <see cref="OpCode.GetProperty"/> arm for <c>version</c>/<c>isEmpty</c>/
/// <c>toString</c>/<c>toUpperString</c>/<c>toCompactString</c>. All chunks are
/// hand-constructed; no compiler dependency.
/// </summary>
public sealed class VirtualMachineGuidTests {
    private const string Canonical = "550e8400-e29b-41d4-a716-446655440000";
    private const string CanonicalUpper = "550E8400-E29B-41D4-A716-446655440000";
    private const string CanonicalCompact = "550e8400e29b41d4a716446655440000";

    private static (VirtualMachine Vm, StringWriter Output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static GrobValue Guid(string canonical) => GuidNatives.FromCanonicalString(canonical);

    // -----------------------------------------------------------------------
    // Value equality — delegates to GrobStruct.Equals; no VM change needed.
    // -----------------------------------------------------------------------

    [Fact]
    public void Equal_TwoGuidsSameValue_IndependentlyConstructed_IsTrue() {
        var chunk = new Chunk();
        byte a = (byte)chunk.AddConstant(Guid(Canonical));
        byte b = (byte)chunk.AddConstant(Guid(Canonical));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(a, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(b, 1);
        chunk.WriteOpCode(OpCode.Equal, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Equal_DifferentGuids_IsFalse() {
        var chunk = new Chunk();
        byte a = (byte)chunk.AddConstant(Guid(Canonical));
        byte b = (byte)chunk.AddConstant(Guid("00000000-0000-0000-0000-000000000000"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(a, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(b, 1);
        chunk.WriteOpCode(OpCode.Equal, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.False(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void Equal_EmptyGuidToItself_IsTrue() {
        var chunk = new Chunk();
        byte a = (byte)chunk.AddConstant(Guid("00000000-0000-0000-0000-000000000000"));
        byte b = (byte)chunk.AddConstant(Guid("00000000-0000-0000-0000-000000000000"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(a, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(b, 1);
        chunk.WriteOpCode(OpCode.Equal, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // GetProperty — version/isEmpty (direct-value properties).
    // -----------------------------------------------------------------------

    [Fact]
    public void GetProperty_Version_ReadsFromCanonicalString() {
        var chunk = new Chunk();
        // 41d4 -> the 4 in the third group is the version nibble: version 4.
        byte g = (byte)chunk.AddConstant(Guid(Canonical));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString("version"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(g, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(4, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void GetProperty_IsEmpty_TrueForAllZeros() {
        var chunk = new Chunk();
        byte g = (byte)chunk.AddConstant(Guid("00000000-0000-0000-0000-000000000000"));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString("isEmpty"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(g, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void GetProperty_IsEmpty_FalseForNonZero() {
        var chunk = new Chunk();
        byte g = (byte)chunk.AddConstant(Guid(Canonical));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString("isEmpty"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(g, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // GetProperty — toString/toUpperString/toCompactString (bound-method binding,
    // mirroring the array higher-order-method precedent).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("toString", Canonical)]
    [InlineData("toUpperString", CanonicalUpper)]
    [InlineData("toCompactString", CanonicalCompact)]
    public void GetProperty_ToStringFamily_BoundMethodCall_RendersExpectedForm(string method, string expected) {
        var chunk = new Chunk();
        byte g = (byte)chunk.AddConstant(Guid(Canonical));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString(method));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(g, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(0, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().IsString);
        Assert.Equal(expected, vm.Stack.Peek().AsString());
    }

    // -----------------------------------------------------------------------
    // ValueDisplay integration (D-336) — a registered toString() renders the canonical
    // string through print(), never the hidden-field structural form.
    // -----------------------------------------------------------------------

    [Fact]
    public void Print_RegisteredToString_RendersCanonicalString_NotHiddenField() {
        var chunk = new Chunk();
        byte g = (byte)chunk.AddConstant(Guid(Canonical));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(g, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, output) = NewVm();
        vm.RegisterToString(GuidNatives.TypeName, v => GuidNatives.CanonicalString(v.AsStruct()));
        vm.Run(chunk);

        string printed = output.ToString();
        Assert.Equal($"{Canonical}{Environment.NewLine}", printed);
        Assert.DoesNotContain(GuidNatives.ValueFieldName, printed);
        Assert.DoesNotContain("[guid]", printed);
    }

    [Fact]
    public void GetProperty_UnknownMember_ThrowsInternalException() {
        // Defensive branch: the type checker rejects an unknown guid member before
        // emission (TypeCheckerGuidTests.UnknownMethod_Call_ReportsSingleE1002), so this
        // is only reachable via hand-built bytecode that bypasses the checker entirely.
        var chunk = new Chunk();
        byte g = (byte)chunk.AddConstant(Guid(Canonical));
        byte nameIdx = (byte)chunk.AddConstant(GrobValue.FromString("nope"));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(g, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();

        Assert.Throws<GrobInternalException>(() => vm.Run(chunk));
    }

    [Fact]
    public void Print_NoRegisteredToString_FallsThroughToStructuralRendering() {
        // Proves the registration path (not some other mechanism) is what makes the
        // canonical-string rendering happen: without RegisterToString, the hidden field
        // leaks through the ordinary structural Struct rendering.
        var chunk = new Chunk();
        byte g = (byte)chunk.AddConstant(Guid(Canonical));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(g, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, output) = NewVm();
        vm.Run(chunk);

        Assert.Contains(GuidNatives.ValueFieldName, output.ToString());
    }
}
