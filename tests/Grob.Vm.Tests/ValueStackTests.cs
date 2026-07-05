using System.Runtime.CompilerServices;
using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// Unit tests for <see cref="ValueStack"/> boundary conditions:
/// underflow guards on Pop/Peek and out-of-range guards on GetSlot/SetSlot.
/// Also covers the pre-Sprint-7 LOH remediation (D-332): default capacity,
/// growth-on-<see cref="ValueStack.Push"/> and the preserved overflow guard.
/// </summary>
public sealed class ValueStackTests {
    // -----------------------------------------------------------------------
    // Pop
    // -----------------------------------------------------------------------

    [Fact]
    public void Pop_EmptyStack_ThrowsGrobInternalException() {
        var stack = new ValueStack();
        Assert.Throws<GrobInternalException>(() => stack.Pop());
    }

    // -----------------------------------------------------------------------
    // Peek
    // -----------------------------------------------------------------------

    [Fact]
    public void Peek_NegativeDistance_ThrowsGrobInternalException() {
        var stack = new ValueStack();
        stack.Push(GrobValue.FromInt(1L), 1);
        Assert.Throws<GrobInternalException>(() => stack.Peek(-1));
    }

    [Fact]
    public void Peek_EmptyStack_ThrowsGrobInternalException() {
        var stack = new ValueStack();
        Assert.Throws<GrobInternalException>(() => stack.Peek());
    }

    // -----------------------------------------------------------------------
    // GetSlot
    // -----------------------------------------------------------------------

    [Fact]
    public void GetSlot_SlotAboveTop_ThrowsGrobInternalException() {
        var stack = new ValueStack();
        Assert.Throws<GrobInternalException>(() => stack.GetSlot(0));
    }

    // -----------------------------------------------------------------------
    // SetSlot
    // -----------------------------------------------------------------------

    [Fact]
    public void SetSlot_SlotAboveTop_ThrowsGrobInternalException() {
        var stack = new ValueStack();
        Assert.Throws<GrobInternalException>(() => stack.SetSlot(0, GrobValue.FromInt(1L)));
    }

    // -----------------------------------------------------------------------
    // D-332: default capacity stays off the Large Object Heap.
    // -----------------------------------------------------------------------

    [Fact]
    public void DefaultCapacity_BackingArrayStaysBelowLohThreshold() {
        // The LOH threshold is 85,000 bytes. GrobValue is a locked 24-byte
        // struct (D-303), so the default-capacity backing array must stay
        // comfortably under that line.
        long backingArrayBytes = (long)Unsafe.SizeOf<GrobValue>() * ValueStack.DefaultCapacity;
        Assert.True(backingArrayBytes < 85_000,
            $"default-capacity backing array is {backingArrayBytes} bytes, at or over the LOH threshold");
    }

    // -----------------------------------------------------------------------
    // D-332: geometric growth on Push preserves values across the boundary.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(1023)]
    [InlineData(1024)]
    [InlineData(1025)]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(4096)]
    [InlineData(16383)]
    [InlineData(16384)]
    public void Push_AcrossGrowthBoundary_PreservesAllValues(int depth) {
        var stack = new ValueStack();
        for (int i = 0; i < depth; i++)
            stack.Push(GrobValue.FromInt(i), 1);

        Assert.Equal(depth, stack.Count);
        for (int i = depth - 1; i >= 0; i--)
            Assert.Equal(i, stack.Pop().AsInt());
    }

    // -----------------------------------------------------------------------
    // D-332: the overflow guard is preserved at the existing cap.
    // -----------------------------------------------------------------------

    [Fact]
    public void Push_BeyondCap_ThrowsE5903() {
        var stack = new ValueStack();
        for (int i = 0; i < ValueStack.Capacity; i++)
            stack.Push(GrobValue.FromInt(i), 1);

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(
            () => stack.Push(GrobValue.FromInt(0), 7));
        Assert.Equal(ErrorCatalog.E5903.Code, ex.Code);
        Assert.Equal(7, ex.Line);
        Assert.Equal(0, ex.Column);
        Assert.Contains("value stack overflow", ex.Message);
    }

    // -----------------------------------------------------------------------
    // D-332 / D-325: an open upvalue survives a backing-array resize.
    // -----------------------------------------------------------------------

    [Fact]
    public void Upvalue_ReadWriteAfterResize_ObservesCorrectValue() {
        var stack = new ValueStack();
        stack.Push(GrobValue.FromInt(42L), 1);   // slot 0 — the captured value
        var upvalue = new Upvalue(stack, 0);

        // Force at least one backing-array resize while the upvalue stays open.
        for (int i = 0; i < ValueStack.DefaultCapacity * 2; i++)
            stack.Push(GrobValue.FromInt(i), 1);

        Assert.Equal(42L, upvalue.Read().AsInt());

        upvalue.Write(GrobValue.FromInt(99L));
        Assert.Equal(99L, upvalue.Read().AsInt());
        Assert.Equal(99L, stack.GetSlot(0).AsInt());
    }
}
