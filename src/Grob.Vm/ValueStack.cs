using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// The VM operand stack: a geometrically-growing array of <see cref="GrobValue"/>
/// slots, capped at <see cref="Capacity"/>. Pushing a primitive (Bool/Int/Float)
/// is a 24-byte struct copy with no heap allocation of its own (D-303, D-304).
///
/// Authority: grob-vm-architecture.md — value stack section.
/// </summary>
public sealed class ValueStack {
    /// <summary>
    /// Initial backing-array size (D-332). 1,024 slots × 24 bytes = 24 KB —
    /// comfortably under the ~85,000-byte Large Object Heap threshold, so a
    /// fresh <see cref="ValueStack"/> (one per <c>VirtualMachine</c> instance)
    /// no longer forces an LOH allocation on every run. Ordinary scripts never
    /// grow past this; <see cref="Push"/> grows geometrically for the rest.
    /// </summary>
    public const int DefaultCapacity = 1024;

    /// <summary>
    /// Maximum simultaneous live values on the operand stack — the effective
    /// call/expression depth cap. Unchanged from the original fixed-capacity
    /// design; <see cref="Push"/> now grows the backing array up to this
    /// ceiling instead of allocating it up front. The value-stack overflow
    /// path surfaces as a runtime error, not an unguarded array write.
    /// </summary>
    public const int Capacity = 16384;

    private GrobValue[] _values = new GrobValue[DefaultCapacity];
    private int _top;

    /// <summary>Number of values currently on the stack.</summary>
    public int Count => _top;

    /// <summary>
    /// Push <paramref name="value"/> onto the top of the stack. When the
    /// backing array is full and below <see cref="Capacity"/>, it grows
    /// geometrically (doubling, capped at <see cref="Capacity"/>) via
    /// <see cref="Array.Resize"/> — existing values are preserved at their
    /// indices, so open upvalues (D-325, which track a stack object plus a
    /// slot index rather than a raw reference) observe the resize
    /// transparently. At <see cref="Capacity"/>, overflow throws
    /// <see cref="GrobRuntimeException"/> carrying <paramref name="line"/>
    /// rather than an unguarded array write — the effective depth cap is
    /// unchanged from before this array was resizable.
    /// </summary>
    public void Push(GrobValue value, int line) {
        if (_top == _values.Length) {
            if (_values.Length == Capacity)
                throw new GrobRuntimeException(ErrorCatalog.E5903.Code, line, "value stack overflow");
            Array.Resize(ref _values, Math.Min(Capacity, _values.Length * 2));
        }
        _values[_top++] = value;
    }

    /// <summary>
    /// Pop and return the top of the stack. Underflow is a compiler/VM bug,
    /// not a user-reachable runtime error — surfaces as
    /// <see cref="GrobInternalException"/>.
    /// </summary>
    public GrobValue Pop() {
        if (_top == 0)
            throw new GrobInternalException("value stack underflow");
        var value = _values[--_top];
        _values[_top] = default;   // release reference slots for GC (D-304)
        return value;
    }

    /// <summary>
    /// Read the value at <paramref name="distance"/> below the top without
    /// popping. <c>distance == 0</c> is the top. Negative distances and
    /// distances past the bottom of the live region are compiler/VM bugs
    /// and surface as <see cref="GrobInternalException"/>.
    /// </summary>
    public GrobValue Peek(int distance = 0) {
        if (distance < 0)
            throw new GrobInternalException("value stack peek with negative distance");
        int index = _top - 1 - distance;
        if (index < 0)
            throw new GrobInternalException("value stack peek underflow");
        return _values[index];
    }

    /// <summary>
    /// Logically empty the stack. Clears the live region (slots <c>0..Count</c>)
    /// via <see cref="Array.Clear(Array, int, int)"/> so any reference values
    /// left over from a prior run are released to the GC (D-304), then resets
    /// the top pointer to zero. Used by <c>VirtualMachine.Run</c> to start each
    /// invocation from a clean operand stack regardless of any leftovers from
    /// a prior exception-terminated run.
    /// </summary>
    internal void Reset() {
        if (_top > 0)
            Array.Clear(_values, 0, _top);   // release reference slots for GC (D-304)
        _top = 0;
    }

    /// <summary>
    /// Read the value at absolute stack slot <paramref name="slot"/> (zero-based).
    /// Used by <see cref="OpCode.GetLocal"/> to load a local variable onto
    /// the top of the operand stack. Out-of-range access is a compiler bug
    /// and surfaces as <see cref="GrobInternalException"/>.
    /// </summary>
    public GrobValue GetSlot(int slot) {
        if ((uint)slot >= (uint)_top)
            throw new GrobInternalException($"GetSlot: slot {slot} out of range (stack top {_top})");
        return _values[slot];
    }

    /// <summary>
    /// Overwrite the value at absolute stack slot <paramref name="slot"/> (zero-based)
    /// with <paramref name="value"/>. Used by <see cref="OpCode.SetLocal"/> to
    /// store a value into a local variable slot. Out-of-range access is a
    /// compiler bug and surfaces as <see cref="GrobInternalException"/>.
    /// </summary>
    public void SetSlot(int slot, GrobValue value) {
        if ((uint)slot >= (uint)_top)
            throw new GrobInternalException($"SetSlot: slot {slot} out of range (stack top {_top})");
        _values[slot] = value;
    }

    /// <summary>
    /// Truncate the live region to exactly <paramref name="count"/> values,
    /// clearing the vacated slots so any reference values are released to the GC
    /// (D-304). Used by <see cref="OpCode.Return"/> to discard a returning frame's
    /// callee value, arguments and locals in one step before the result is pushed.
    /// The caller guarantees <paramref name="count"/> is within the current live
    /// region (a returning call frame always sits above its caller's stack base).
    /// </summary>
    internal void TrimToCount(int count) {
        if (count < _top)
            Array.Clear(_values, count, _top - count);   // release reference slots for GC (D-304)
        _top = count;
    }

    /// <summary>
    /// Snapshot the live region of the stack — used by the
    /// <c>#if DEBUG</c> trace hook to render the stack each iteration.
    /// </summary>
    internal ReadOnlySpan<GrobValue> AsSpan() => _values.AsSpan(0, _top);
}
