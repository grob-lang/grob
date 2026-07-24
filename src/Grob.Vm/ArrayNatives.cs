using System.Collections.Generic;
using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Factory for the array higher-order method natives: <c>filter</c>, <c>select</c>,
/// <c>sort</c> and <c>each</c>.  Each method is bound to its receiver array at
/// <see cref="OpCode.GetProperty"/> dispatch time, capturing the array and the
/// <see cref="VmInvoker"/> callback in the returned <see cref="NativeFunction"/>
/// delegate.  Sprint 5 Increment C; moved to <c>Grob.Stdlib</c> in Sprint 6+.
/// </summary>
internal static class ArrayNatives {
    /// <summary>
    /// Returns the bound <see cref="NativeFunction"/> for the given
    /// <paramref name="methodName"/> on <paramref name="receiver"/>, or
    /// <see langword="null"/> when the name is not an array higher-order method.
    /// The <paramref name="invoker"/> is captured in the native's delegate so
    /// the implementation can call back into the VM to run the lambda argument.
    /// </summary>
    internal static NativeFunction? GetMethod(
            string methodName, GrobArray receiver, VmInvoker invoker) =>
        methodName switch {
            "filter" => new NativeFunction("filter", 1,
                (args, inv) => Filter(args, inv, receiver)),
            "select" => new NativeFunction("select", 1,
                (args, inv) => Select(args, inv, receiver)),
            "sort" => new NativeFunction("sort", 1,
                (args, inv) => Sort(args, inv, receiver)),
            "each" => new NativeFunction("each", 1,
                (args, inv) => Each(args, inv, receiver)),
            // Sprint 9 Increment C0a-1 (D-371): the non-higher-order query members —
            // none take a function argument, so each ignores the VmInvoker parameter.
            "first" => new NativeFunction("first", 0,
                (_, _) => First(receiver)),
            "last" => new NativeFunction("last", 0,
                (_, _) => Last(receiver)),
            "contains" => new NativeFunction("contains", 1,
                (args, _) => Contains(args, receiver)),
            _ => null,
        };

    // -----------------------------------------------------------------------
    // filter(fn: T → bool) → T[]
    // -----------------------------------------------------------------------

    private static GrobValue Filter(GrobValue[] args, VmInvoker invoker, GrobArray source) {
        GrobValue fn = args[0];
        var result = new List<GrobValue>(source.Count);
        for (int i = 0; i < source.Count; i++) {
            GrobValue element = source[i];
            GrobValue keep = invoker(fn, [element]);
            if (keep.AsBool())
                result.Add(element);
        }
        // Pass the List directly — GrobArray takes IEnumerable<GrobValue> and copies
        // once; a [.. result] spread would add a redundant intermediate array.
        return GrobValue.FromArray(new GrobArray(result));
    }

    // -----------------------------------------------------------------------
    // select(fn: T → U) → U[]
    // -----------------------------------------------------------------------

    private static GrobValue Select(GrobValue[] args, VmInvoker invoker, GrobArray source) {
        GrobValue fn = args[0];
        var result = new GrobValue[source.Count];
        for (int i = 0; i < source.Count; i++)
            result[i] = invoker(fn, [source[i]]);
        return GrobValue.FromArray(new GrobArray(result));
    }

    // -----------------------------------------------------------------------
    // sort(fn: T → U, descending: bool = false) → T[]
    // Stable via LINQ OrderBy (D-281).
    // -----------------------------------------------------------------------

    private static GrobValue Sort(GrobValue[] args, VmInvoker invoker, GrobArray source) {
        GrobValue fn = args[0];
        bool descending = args.Length >= 2 && args[1].AsBool();

        // Project each element to a sort key.
        var pairs = new (GrobValue element, GrobValue key)[source.Count];
        for (int i = 0; i < source.Count; i++)
            pairs[i] = (source[i], invoker(fn, [source[i]]));

        // Stable sort via LINQ (preserves relative order of equal-key elements).
        var comparer = GrobValueComparer.Instance;
        IEnumerable<(GrobValue element, GrobValue key)> sorted = descending
            ? pairs.OrderByDescending(p => p.key, comparer)
            : pairs.OrderBy(p => p.key, comparer);

        // The underlying sort wraps a comparer exception in InvalidOperationException
        // ("Failed to compare two elements in the array"). Unwrap so a key-type fault
        // surfaces as the GrobRuntimeException the comparer raised, not a .NET internal.
        GrobValue[] elements;
        try {
            elements = sorted.Select(p => p.element).ToArray();
        } catch (InvalidOperationException ex) when (ex.InnerException is GrobRuntimeException inner) {
            throw inner;
        }
        return GrobValue.FromArray(new GrobArray(elements));
    }

    // -----------------------------------------------------------------------
    // each(fn: T → void) → void (returns nil)
    // -----------------------------------------------------------------------

    private static GrobValue Each(GrobValue[] args, VmInvoker invoker, GrobArray source) {
        GrobValue fn = args[0];
        for (int i = 0; i < source.Count; i++)
            invoker(fn, [source[i]]);
        return GrobValue.Nil;
    }

    // -----------------------------------------------------------------------
    // first() → T?, last() → T? — nil on an empty array (Sprint 9 Increment C0a-1).
    // -----------------------------------------------------------------------

    private static GrobValue First(GrobArray source) =>
        source.Count == 0 ? GrobValue.Nil : source[0];

    private static GrobValue Last(GrobArray source) =>
        source.Count == 0 ? GrobValue.Nil : source[source.Count - 1];

    // -----------------------------------------------------------------------
    // contains(v: T) → bool (Sprint 9 Increment C0a-1).
    // -----------------------------------------------------------------------

    private static GrobValue Contains(GrobValue[] args, GrobArray source) {
        GrobValue needle = args[0];
        for (int i = 0; i < source.Count; i++) {
            if (ValuesEqual(source[i], needle)) return GrobValue.FromBool(true);
        }
        return GrobValue.FromBool(false);
    }

    /// <summary>
    /// The same equality <c>==</c> uses at runtime, so <c>contains</c> can never
    /// disagree with it — most notably for <c>date</c>, whose equality is
    /// instant-based (D-367, <see cref="OpCode.EqualDate"/>'s VM handler) rather than
    /// the field-by-field <c>__value</c> compare <see cref="GrobValue"/>'s own
    /// <c>operator==</c> would otherwise apply. Every other kind, including <c>guid</c>
    /// (D-169's field-by-field struct equality), is already correct via
    /// <see cref="GrobValue"/>'s own operator.
    /// </summary>
    private static bool ValuesEqual(GrobValue a, GrobValue b) {
        if (a.TryAsStruct(out GrobStruct? sa) && b.TryAsStruct(out GrobStruct? sb)
                && sa!.TypeName == DateNatives.TypeName && sb!.TypeName == DateNatives.TypeName) {
            return a.IsNil || b.IsNil
                ? a.IsNil && b.IsNil
                : DateNatives.ToDateTimeOffset(sa) == DateNatives.ToDateTimeOffset(sb);
        }
        return a == b;
    }
}

// ---------------------------------------------------------------------------
// Comparable key ordering for sort (D-281)
// ---------------------------------------------------------------------------

/// <summary>
/// Orders <see cref="GrobValue"/> sort keys. Supports Int (long), Float (double),
/// String (ordinal), Bool (false &lt; true), and — Sprint 9 Increment C0a-1 (D-371) —
/// the two <c>Struct</c>-kind named types the registry advertises as
/// <c>Comparable</c>: <c>date</c> (instant basis, <see cref="DateNatives.ToDateTimeOffset"/>)
/// and <c>guid</c> (ordinal on the canonical string). Every other kind, including any
/// other <c>Struct</c> (a user type, or a mixed <c>date</c>/<c>guid</c> pairing) throws
/// <see cref="GrobRuntimeException"/>.
/// </summary>
internal sealed class GrobValueComparer : IComparer<GrobValue> {
    internal static readonly GrobValueComparer Instance = new();

    // The comparer runs deep inside LINQ's sort and has no access to the call site's
    // source location, so faults carry the minimum valid line (1) and column 0.
    // GrobRuntimeException requires line >= 1; once Increment D adds compile-time
    // Comparable validation these runtime faults become unreachable in well-typed code.
    private const int UnknownLine = 1;
    private const int UnknownColumn = 0;

    public int Compare(GrobValue x, GrobValue y) {
        if (x.Kind != y.Kind)
            throw new GrobRuntimeException(
                ErrorCatalog.E0004.Code, UnknownLine, UnknownColumn,
                $"sort key type mismatch: cannot compare {x.Kind} with {y.Kind}");

        return x.Kind switch {
            GrobValueKind.Int => x.AsInt().CompareTo(y.AsInt()),
            GrobValueKind.Float => x.AsFloat().CompareTo(y.AsFloat()),
            GrobValueKind.String => string.CompareOrdinal(x.AsString(), y.AsString()),
            GrobValueKind.Bool => x.AsBool().CompareTo(y.AsBool()),
            GrobValueKind.Struct => CompareStruct(x.AsStruct(), y.AsStruct()),
            _ => throw new GrobRuntimeException(
                     ErrorCatalog.E0004.Code, UnknownLine, UnknownColumn,
                     $"sort key type {x.Kind} does not implement Comparable"),
        };
    }

    /// <summary>
    /// Orders two <c>Struct</c>-kind sort keys, discriminated by
    /// <see cref="GrobStruct.TypeName"/>. <c>date</c> MUST compare via
    /// <see cref="DateNatives.ToDateTimeOffset"/> — the same instant basis
    /// <see cref="OpCode.LessDate"/>/<see cref="OpCode.GreaterDate"/>/
    /// <see cref="OpCode.EqualDate"/> already share (D-367) — never the raw
    /// <c>__value</c> string, which would order dates differently from <c>&lt;</c> and
    /// reintroduce the exact trichotomy incoherence D-367 closed. <c>guid</c> stays
    /// ordinal on its canonical string (D-357). Any other pairing — a user struct, or a
    /// mixed <c>date</c>/<c>guid</c> pairing — does not implement <c>Comparable</c>.
    /// </summary>
    private static int CompareStruct(GrobStruct a, GrobStruct b) {
        if (a.TypeName == DateNatives.TypeName && b.TypeName == DateNatives.TypeName) {
            return DateNatives.ToDateTimeOffset(a).CompareTo(DateNatives.ToDateTimeOffset(b));
        }
        if (a.TypeName == GuidNatives.TypeName && b.TypeName == GuidNatives.TypeName) {
            return string.CompareOrdinal(GuidNatives.CanonicalString(a), GuidNatives.CanonicalString(b));
        }
        throw new GrobRuntimeException(ErrorCatalog.E0004.Code, UnknownLine, UnknownColumn,
            $"sort key type {a.TypeName} does not implement Comparable");
    }
}
