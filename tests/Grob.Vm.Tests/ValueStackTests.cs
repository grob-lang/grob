using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// Unit tests for <see cref="ValueStack"/> boundary conditions:
/// underflow guards on Pop/Peek and out-of-range guards on GetSlot/SetSlot.
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
}
