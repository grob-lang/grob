using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// The VM operand stack: a fixed-capacity array of <see cref="GrobValue"/>
/// slots. Pushing a primitive (Bool/Int/Float) is a 24-byte struct copy with
/// no allocation (D-303, D-304).
///
/// Authority: grob-vm-architecture.md — value stack section.
/// </summary>
public sealed class ValueStack {
    /// <summary>
    /// Maximum simultaneous live values on the operand stack. Chosen to comfortably
    /// exceed Sprint 2's needs (no call frames yet) while leaving headroom for
    /// future locals and intermediate computation. The value-stack overflow
    /// path surfaces as a runtime error, not an unguarded array write.
    /// </summary>
    public const int Capacity = 16384;

    private readonly GrobValue[] _values = new GrobValue[Capacity];
    private int _top;

    /// <summary>Number of values currently on the stack.</summary>
    public int Count => _top;

    /// <summary>
    /// Push <paramref name="value"/> onto the top of the stack. On overflow
    /// throws <see cref="GrobRuntimeException"/> carrying <paramref name="line"/>
    /// rather than an unguarded array write.
    /// </summary>
    public void Push(GrobValue value, int line) {
        if (_top == _values.Length)
            throw new GrobRuntimeException("E5903", line, "value stack overflow");
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
    /// Logically empty the stack. Sets the top pointer to zero without
    /// clearing slots — the next <see cref="Push"/> will overwrite, and
    /// any reference values left over are released by the per-<see cref="Pop"/>
    /// slot clear once <see cref="Pop"/> is invoked. Used by
    /// <c>VirtualMachine.Run</c> to start each invocation from a clean
    /// operand stack regardless of any leftovers from a prior
    /// exception-terminated run.
    /// </summary>
    internal void Reset() {
        if (_top > 0)
            Array.Clear(_values, 0, _top);   // release reference slots for GC (D-304)
        _top = 0;
    }

    /// <summary>
    /// Snapshot the live region of the stack — used by the
    /// <c>#if DEBUG</c> trace hook to render the stack each iteration.
    /// </summary>
    internal ReadOnlySpan<GrobValue> AsSpan() => _values.AsSpan(0, _top);
}
