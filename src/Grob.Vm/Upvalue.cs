using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// An indirection cell that allows a closure to capture a variable from an
/// enclosing function's stack frame (Sprint 5 Increment D).
/// </summary>
/// <remarks>
/// <para>While the enclosing function is executing, the upvalue is <b>open</b>: it
/// holds a reference to the <see cref="ValueStack"/> and an absolute slot index so
/// <see cref="OpCode.GetUpvalue"/> and <see cref="OpCode.SetUpvalue"/> read and write directly
/// through to the live stack slot. Two closures capturing the same slot from the
/// same enclosing call share one <see cref="Upvalue"/> object and therefore see each
/// other's writes.</para>
/// <para>When the enclosing function returns, <see cref="Close"/> copies the value
/// off the stack into a private heap field and sets the slot index to −1 (<b>closed</b>
/// state). Subsequent reads and writes operate on the heap copy, so the closure keeps
/// the variable alive independently of the (now gone) enclosing frame.</para>
/// <para><see cref="Read"/> and <see cref="Write"/> are transparent across the
/// open→closed transition — callers in <see cref="OpCode.GetUpvalue"/> and
/// <see cref="OpCode.SetUpvalue"/> arms never need to test <see cref="IsClosed"/>.</para>
/// </remarks>
internal sealed class Upvalue {
    private ValueStack? _stack;     // non-null while open
    private int _slotIndex;          // absolute stack slot; set to -1 when closed
    private GrobValue _closedValue; // heap copy populated by Close()

    /// <summary>
    /// Creates an open upvalue tracking absolute stack slot
    /// <paramref name="slotIndex"/> in <paramref name="stack"/>.
    /// </summary>
    internal Upvalue(ValueStack stack, int slotIndex) {
        _stack = stack;
        _slotIndex = slotIndex;
    }

    /// <summary>
    /// <see langword="true"/> once <see cref="Close"/> has been called — the value
    /// now lives in the heap field, not on the stack.
    /// </summary>
    internal bool IsClosed => _slotIndex < 0;

    /// <summary>
    /// The absolute stack slot tracked by this (still open) upvalue.
    /// Used by <see cref="VirtualMachine"/> to key the open-upvalue list and to
    /// avoid creating duplicate upvalues for the same slot.
    /// Only valid when <see cref="IsClosed"/> is <see langword="false"/>.
    /// </summary>
    internal int SlotIndex => _slotIndex;

    /// <summary>
    /// Returns the captured variable's current value — from the live stack slot
    /// if open, from the heap copy if closed.
    /// </summary>
    internal GrobValue Read() =>
        IsClosed ? _closedValue : _stack!.GetSlot(_slotIndex);

    /// <summary>
    /// Overwrites the captured variable with <paramref name="value"/> — to the
    /// live stack slot if open, to the heap copy if closed.
    /// </summary>
    internal void Write(GrobValue value) {
        if (IsClosed)
            _closedValue = value;
        else
            _stack!.SetSlot(_slotIndex, value);
    }

    /// <summary>
    /// Transitions the upvalue from open to closed by copying the current stack
    /// value into the heap field and detaching from the stack. Called by
    /// <see cref="VirtualMachine.CloseUpvaluesFrom"/> in the
    /// <see cref="OpCode.Return"/> path before the enclosing frame's slots are
    /// discarded.
    /// </summary>
    internal void Close() {
        _closedValue = _stack!.GetSlot(_slotIndex);
        _slotIndex = -1;
        _stack = null;
    }
}
