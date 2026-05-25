using System.Runtime.CompilerServices;

namespace Grob.Core;

/// <summary>
/// The universal Grob runtime value. A hand-rolled tagged-union <c>readonly struct</c>.
/// Representation locked May 2026 — D-303, OQ-005 closed.
///
/// Layout (x64): 24 bytes — 1-byte discriminator, 7 bytes padding,
/// 8-byte scalar slot, 8-byte managed reference slot.
/// The reference slot is the only GC-visible field. Primitives live in
/// the scalar slot and never touch the heap.
///
/// <c>default(GrobValue)</c> is <see cref="Nil"/> — intentional.
/// </summary>
public readonly struct GrobValue : IEquatable<GrobValue> {
    // Representation locked May 2026 (D-303, OQ-005 closed).
    // Tagged union: discriminator + scalar slot + reference slot.
    // Do NOT access these fields outside Grob.Core. The public API below
    // is the encapsulation boundary.
    private readonly GrobValueKind _kind;
    private readonly long _scalar;
    private readonly object? _reference;

    private GrobValue(GrobValueKind kind, long scalar, object? reference) {
        _kind = kind;
        _scalar = scalar;
        _reference = reference;
    }

    // ----- Singleton -----

    /// <summary>The Grob nil value. Equal to <c>default(GrobValue)</c>.</summary>
    public static readonly GrobValue Nil;   // == default(GrobValue)

    // ----- Factories -----

    /// <summary>Creates a <see cref="GrobValueKind.Bool"/> value from a C# <see cref="bool"/>.</summary>
    public static GrobValue FromBool(bool value) => new(GrobValueKind.Bool, value ? 1L : 0L, null);

    /// <summary>Creates a <see cref="GrobValueKind.Int"/> value from a <see cref="long"/>.</summary>
    public static GrobValue FromInt(long value) => new(GrobValueKind.Int, value, null);

    /// <summary>
    /// Creates a <see cref="GrobValueKind.Float"/> value from a <see cref="double"/>.
    /// The raw bit pattern is stored in the scalar slot via
    /// <see cref="BitConverter.DoubleToInt64Bits(double)"/>.
    /// </summary>
    public static GrobValue FromFloat(double value) => new(GrobValueKind.Float, BitConverter.DoubleToInt64Bits(value), null);

    /// <summary>Creates a <see cref="GrobValueKind.String"/> value. The string is stored as a managed reference.</summary>
    public static GrobValue FromString(string value) => new(GrobValueKind.String, 0L, value);

    /// <summary>Creates a <see cref="GrobValueKind.Array"/> value wrapping <paramref name="value"/>.</summary>
    public static GrobValue FromArray(GrobArray value) => new(GrobValueKind.Array, 0L, value);

    /// <summary>Creates a <see cref="GrobValueKind.Map"/> value wrapping <paramref name="value"/>.</summary>
    public static GrobValue FromMap(GrobMap value) => new(GrobValueKind.Map, 0L, value);

    /// <summary>Creates a <see cref="GrobValueKind.Struct"/> value wrapping <paramref name="value"/>.</summary>
    public static GrobValue FromStruct(GrobStruct value) => new(GrobValueKind.Struct, 0L, value);

    /// <summary>Creates a <see cref="GrobValueKind.Function"/> value wrapping <paramref name="value"/>.</summary>
    public static GrobValue FromFunction(GrobFunction value) => new(GrobValueKind.Function, 0L, value);

    // ----- Inspection -----

    /// <summary>The runtime kind discriminator for this value.</summary>
    public GrobValueKind Kind => _kind;

    /// <summary><c>true</c> when this value is <see cref="GrobValueKind.Nil"/>.</summary>
    public bool IsNil => _kind == GrobValueKind.Nil;

    /// <summary><c>true</c> when this value is <see cref="GrobValueKind.Bool"/>.</summary>
    public bool IsBool => _kind == GrobValueKind.Bool;

    /// <summary><c>true</c> when this value is <see cref="GrobValueKind.Int"/>.</summary>
    public bool IsInt => _kind == GrobValueKind.Int;

    /// <summary><c>true</c> when this value is <see cref="GrobValueKind.Float"/>.</summary>
    public bool IsFloat => _kind == GrobValueKind.Float;

    /// <summary><c>true</c> when this value is <see cref="GrobValueKind.String"/>.</summary>
    public bool IsString => _kind == GrobValueKind.String;

    /// <summary><c>true</c> when this value is <see cref="GrobValueKind.Array"/>.</summary>
    public bool IsArray => _kind == GrobValueKind.Array;

    /// <summary><c>true</c> when this value is <see cref="GrobValueKind.Map"/>.</summary>
    public bool IsMap => _kind == GrobValueKind.Map;

    /// <summary><c>true</c> when this value is <see cref="GrobValueKind.Struct"/>.</summary>
    public bool IsStruct => _kind == GrobValueKind.Struct;

    /// <summary><c>true</c> when this value is <see cref="GrobValueKind.Function"/>.</summary>
    public bool IsFunction => _kind == GrobValueKind.Function;

    // ----- Strict accessors — throw GrobInternalException on kind mismatch -----

    /// <summary>Returns the inner <see cref="bool"/>. Throws <see cref="GrobInternalException"/> if not Bool.</summary>
    public bool AsBool() => Strict(GrobValueKind.Bool) ? _scalar != 0 : default;

    /// <summary>Returns the inner <see cref="long"/>. Throws <see cref="GrobInternalException"/> if not Int.</summary>
    public long AsInt() => Strict(GrobValueKind.Int) ? _scalar : default;

    /// <summary>Returns the inner <see cref="double"/>. Throws <see cref="GrobInternalException"/> if not Float.</summary>
    public double AsFloat() => Strict(GrobValueKind.Float) ? BitConverter.Int64BitsToDouble(_scalar) : default;

    /// <summary>Returns the inner <see cref="string"/>. Throws <see cref="GrobInternalException"/> if not String.</summary>
    public string AsString() => Strict(GrobValueKind.String) ? (string)_reference! : default!;

    /// <summary>Returns the inner <see cref="GrobArray"/>. Throws <see cref="GrobInternalException"/> if not Array.</summary>
    public GrobArray AsArray() => Strict(GrobValueKind.Array) ? (GrobArray)_reference! : default!;

    /// <summary>Returns the inner <see cref="GrobMap"/>. Throws <see cref="GrobInternalException"/> if not Map.</summary>
    public GrobMap AsMap() => Strict(GrobValueKind.Map) ? (GrobMap)_reference! : default!;

    /// <summary>Returns the inner <see cref="GrobStruct"/>. Throws <see cref="GrobInternalException"/> if not Struct.</summary>
    public GrobStruct AsStruct() => Strict(GrobValueKind.Struct) ? (GrobStruct)_reference! : default!;

    /// <summary>Returns the inner <see cref="GrobFunction"/>. Throws <see cref="GrobInternalException"/> if not Function.</summary>
    public GrobFunction AsFunction() => Strict(GrobValueKind.Function) ? (GrobFunction)_reference! : default!;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool Strict(GrobValueKind expected) {
        if (_kind != expected)
            throw new GrobInternalException(
                $"GrobValue kind mismatch: expected {expected}, actual {_kind}.");
        return true;
    }

    // ----- Try-accessors — return false on kind mismatch, no exception -----

    /// <summary>Attempts to extract a <see cref="bool"/>. Returns <c>false</c> if not Bool.</summary>
    public bool TryAsBool(out bool value) { if (_kind == GrobValueKind.Bool) { value = _scalar != 0; return true; } value = default; return false; }

    /// <summary>Attempts to extract a <see cref="long"/>. Returns <c>false</c> if not Int.</summary>
    public bool TryAsInt(out long value) { if (_kind == GrobValueKind.Int) { value = _scalar; return true; } value = default; return false; }

    /// <summary>Attempts to extract a <see cref="double"/>. Returns <c>false</c> if not Float.</summary>
    public bool TryAsFloat(out double value) { if (_kind == GrobValueKind.Float) { value = BitConverter.Int64BitsToDouble(_scalar); return true; } value = default; return false; }

    /// <summary>Attempts to extract a <see cref="string"/>. Returns <c>false</c> if not String.</summary>
    public bool TryAsString(out string? value) { if (_kind == GrobValueKind.String) { value = (string?)_reference; return true; } value = default; return false; }

    /// <summary>Attempts to extract a <see cref="GrobArray"/>. Returns <c>false</c> if not Array.</summary>
    public bool TryAsArray(out GrobArray? value) { if (_kind == GrobValueKind.Array) { value = (GrobArray?)_reference; return true; } value = default; return false; }

    /// <summary>Attempts to extract a <see cref="GrobMap"/>. Returns <c>false</c> if not Map.</summary>
    public bool TryAsMap(out GrobMap? value) { if (_kind == GrobValueKind.Map) { value = (GrobMap?)_reference; return true; } value = default; return false; }

    /// <summary>Attempts to extract a <see cref="GrobStruct"/>. Returns <c>false</c> if not Struct.</summary>
    public bool TryAsStruct(out GrobStruct? value) { if (_kind == GrobValueKind.Struct) { value = (GrobStruct?)_reference; return true; } value = default; return false; }

    /// <summary>Attempts to extract a <see cref="GrobFunction"/>. Returns <c>false</c> if not Function.</summary>
    public bool TryAsFunction(out GrobFunction? value) { if (_kind == GrobValueKind.Function) { value = (GrobFunction?)_reference; return true; } value = default; return false; }

    // ----- Equality and hashing -----

    /// <summary>
    /// Value equality used by the runtime and collections.
    /// <para>
    /// For <see cref="GrobValueKind.Float"/> values this uses
    /// <see cref="double.Equals(double)"/> so that <c>NaN.Equals(NaN)</c> returns
    /// <c>true</c> — the deliberate inconsistency that lets collections locate NaN keys.
    /// Use <c>==</c> for IEEE 754 semantics.
    /// </para>
    /// </summary>
    public bool Equals(GrobValue other) {
        if (_kind != other._kind) return false;
        return _kind switch {
            GrobValueKind.Nil => true,
            GrobValueKind.Bool => _scalar == other._scalar,
            GrobValueKind.Int => _scalar == other._scalar,
            GrobValueKind.Float => BitConverter.Int64BitsToDouble(_scalar)
                                          .Equals(BitConverter.Int64BitsToDouble(other._scalar)),
            GrobValueKind.String => string.Equals(
                                          (string?)_reference,
                                          (string?)other._reference,
                                          StringComparison.Ordinal),
            GrobValueKind.Array => ReferenceEquals(_reference, other._reference),
            GrobValueKind.Map => ReferenceEquals(_reference, other._reference),
            GrobValueKind.Struct => ((GrobStruct?)_reference)?.Equals((GrobStruct?)other._reference) ?? other._reference is null,
            GrobValueKind.Function => ReferenceEquals(_reference, other._reference),
            _ => false,
        };
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GrobValue other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() {
        return _kind switch {
            GrobValueKind.Nil => HashCode.Combine(GrobValueKind.Nil),
            GrobValueKind.Bool => HashCode.Combine(GrobValueKind.Bool, _scalar),
            GrobValueKind.Int => HashCode.Combine(GrobValueKind.Int, _scalar),
            GrobValueKind.Float => HashCode.Combine(GrobValueKind.Float, BitConverter.Int64BitsToDouble(_scalar).GetHashCode()),
            GrobValueKind.String => HashCode.Combine(GrobValueKind.String, StringComparer.Ordinal.GetHashCode((string?)_reference ?? string.Empty)),
            GrobValueKind.Array => HashCode.Combine(GrobValueKind.Array, RuntimeHelpers.GetHashCode(_reference)),
            GrobValueKind.Map => HashCode.Combine(GrobValueKind.Map, RuntimeHelpers.GetHashCode(_reference)),
            GrobValueKind.Struct => HashCode.Combine(GrobValueKind.Struct, _reference?.GetHashCode() ?? 0),
            GrobValueKind.Function => HashCode.Combine(GrobValueKind.Function, RuntimeHelpers.GetHashCode(_reference)),
            _ => 0,
        };
    }

    /// <summary>
    /// IEEE 754 equality for <see cref="GrobValueKind.Float"/> (<c>NaN != NaN</c>,
    /// <c>+0.0 == -0.0</c>). All other kinds delegate to <see cref="Equals(GrobValue)"/>.
    /// </summary>
    public static bool operator ==(GrobValue left, GrobValue right) {
        if (left._kind == GrobValueKind.Float && right._kind == GrobValueKind.Float)
            return BitConverter.Int64BitsToDouble(left._scalar) == BitConverter.Int64BitsToDouble(right._scalar);
        return left.Equals(right);
    }

    /// <summary>Logical negation of <c>==</c>.</summary>
    public static bool operator !=(GrobValue left, GrobValue right) => !(left == right);

    // ----- Display -----

    /// <summary>
    /// Human-readable representation used by the disassembler and <c>print()</c>.
    /// </summary>
    public override string ToString() => _kind switch {
        GrobValueKind.Nil => "nil",
        GrobValueKind.Bool => _scalar != 0 ? "true" : "false",
        GrobValueKind.Int => _scalar.ToString(),
        GrobValueKind.Float => BitConverter.Int64BitsToDouble(_scalar).ToString("G"),
        GrobValueKind.String => (string?)_reference ?? string.Empty,
        GrobValueKind.Array => $"[array({((GrobArray?)_reference)?.Count ?? 0})]",
        GrobValueKind.Map => "[map]",
        GrobValueKind.Struct => $"[{((GrobStruct?)_reference)?.TypeName ?? "struct"}]",
        GrobValueKind.Function => $"<fn {((GrobFunction?)_reference)?.Name ?? "?"}>",
        _ => "[unknown]",
    };
}
