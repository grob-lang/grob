using System.Collections.Generic;
using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Factory for the array method natives: the higher-order members <c>filter</c>,
/// <c>select</c>, <c>sort</c> and <c>each</c>; the non-higher-order query members
/// <c>first</c>, <c>last</c> and <c>contains</c> (Sprint 9 Increment C0a-1, D-371); and
/// the in-place-mutating members <c>append</c>, <c>insert</c>, <c>remove</c>, <c>clear</c>
/// (Sprint 9 Increment C0a-2, D-373), completing the <c>T[]</c> surface. Each method is
/// bound to its receiver array at <see cref="OpCode.GetProperty"/> dispatch time,
/// capturing the array in the returned <see cref="NativeFunction"/> delegate.
/// <see cref="GetMethod"/> itself takes no <see cref="VmInvoker"/> — mirroring
/// <see cref="MapNatives.GetMethod"/> (D-394). The four higher-order members still run a
/// lambda argument, but they take their <see cref="VmInvoker"/> from their own
/// <see cref="NativeFunction.Implementation"/> delegate's second parameter, supplied by
/// the VM's <c>Call</c> handler at invocation time — never from a bind-time parameter —
/// so no invoker needs binding or capturing here. Sprint 5 Increment C; moved to
/// <c>Grob.Stdlib</c> in Sprint 6+.
/// </summary>
internal static class ArrayNatives {
    /// <summary>The <c>GrobError</c> leaf <c>insert</c>/<c>remove</c> raise through the
    /// native-throw seam on an out-of-range index — the same leaf the array indexer and
    /// <c>StringMethodsPlugin</c>'s range-bound members already use (D-373).</summary>
    private const string IndexErrorLeaf = "IndexError";

    /// <summary>
    /// Returns the bound <see cref="NativeFunction"/> for the given
    /// <paramref name="methodName"/> on <paramref name="receiver"/>, or
    /// <see langword="null"/> when the name is not a recognised array method. Takes no
    /// <see cref="VmInvoker"/> (D-394): the four higher-order members
    /// (<c>filter</c>/<c>select</c>/<c>sort</c>/<c>each</c>) receive theirs through their
    /// bound <see cref="NativeFunction.Implementation"/> delegate's own second parameter
    /// at invocation time, so binding one here purely for signature parity would allocate
    /// a capturing delegate and a <c>FinallyContext</c> on every array property dispatch
    /// for a parameter nothing reads — the same reasoning
    /// <see cref="MapNatives.GetMethod"/>'s doc comment already records.
    /// </summary>
    internal static NativeFunction? GetMethod(string methodName, GrobArray receiver) =>
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
            // Sprint 9 Increment C0a-2 (D-373): the in-place-mutating members — none
            // take a function argument either.
            "append" => new NativeFunction("append", 1,
                (args, _) => Append(args, receiver)),
            "insert" => new NativeFunction("insert", 2,
                (args, _) => Insert(args, receiver)),
            "remove" => new NativeFunction("remove", 1,
                (args, _) => Remove(args, receiver)),
            "clear" => new NativeFunction("clear", 0,
                (_, _) => Clear(receiver)),
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
        // surfaces as the NativeFaultException the comparer raised (correctness batch,
        // D-382), routing it through the same native-throw seam insert/remove and the
        // string natives use, rather than a .NET internal.
        GrobValue[] elements;
        try {
            elements = sorted.Select(p => p.element).ToArray();
        } catch (InvalidOperationException ex) when (ex.InnerException is NativeFaultException inner) {
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

    // -----------------------------------------------------------------------
    // append(value: T) -> void — mutates in place (Sprint 9 Increment C0a-2, D-373).
    // Reference semantics (D-372): the receiver GrobArray is the same instance every
    // other binding aliasing it observes, so the mutation is visible everywhere.
    // -----------------------------------------------------------------------

    private static GrobValue Append(GrobValue[] args, GrobArray receiver) {
        receiver.Add(args[0]);
        return GrobValue.Nil;
    }

    // -----------------------------------------------------------------------
    // insert(index: int, value: T) -> void — mutates in place (D-373). Pinned boundary
    // rule: index == receiver.Count is a VALID append-position insert (matches
    // List<T>.Insert's own convention, and Python/JS array-insert conventions); index < 0
    // or index > Count throws IndexError/E5101.
    // -----------------------------------------------------------------------

    private static GrobValue Insert(GrobValue[] args, GrobArray receiver) {
        long index = args[0].AsInt();
        if (index < 0 || index > receiver.Count) {
            throw new NativeFaultException(IndexErrorLeaf, ErrorCatalog.E5101.Code,
                $"insert: index {index} is out of range for an array of length {receiver.Count}.");
        }
        receiver.Insert((int)index, args[1]);
        return GrobValue.Nil;
    }

    // -----------------------------------------------------------------------
    // remove(index: int) -> void — mutates in place (D-373). Strict bounds: unlike
    // insert, index == receiver.Count is OUT of range — there is no element there.
    // -----------------------------------------------------------------------

    private static GrobValue Remove(GrobValue[] args, GrobArray receiver) {
        long index = args[0].AsInt();
        if (index < 0 || index >= receiver.Count) {
            throw new NativeFaultException(IndexErrorLeaf, ErrorCatalog.E5101.Code,
                $"remove: index {index} is out of range for an array of length {receiver.Count}.");
        }
        receiver.RemoveAt((int)index);
        return GrobValue.Nil;
    }

    // -----------------------------------------------------------------------
    // clear() -> void — mutates in place (D-373).
    // -----------------------------------------------------------------------

    private static GrobValue Clear(GrobArray receiver) {
        receiver.Clear();
        return GrobValue.Nil;
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
/// other <c>Struct</c> (a user type, or a mixed <c>date</c>/<c>guid</c> pairing) raises
/// <c>RuntimeError</c>/<c>E5906</c> through <see cref="NativeFaultException"/>
/// (correctness batch, D-382 — previously the compile-time <c>E0004</c>, and previously
/// bypassed the native-throw seam entirely).
/// </summary>
internal sealed class GrobValueComparer : IComparer<GrobValue> {
    internal static readonly GrobValueComparer Instance = new();

    // The GrobError leaf an uncomparable sort key raises through the native-throw seam
    // (correctness batch, D-382): a runtime type failure with no better-fitting domain
    // family, not the compile-time E0004 the comparer previously (and wrongly) reused.
    private const string RuntimeErrorLeaf = "RuntimeError";

    public int Compare(GrobValue x, GrobValue y) {
        if (x.Kind != y.Kind)
            throw new NativeFaultException(RuntimeErrorLeaf, ErrorCatalog.E5906.Code,
                $"sort key type mismatch: cannot compare {x.Kind} with {y.Kind}");

        return x.Kind switch {
            GrobValueKind.Int => x.AsInt().CompareTo(y.AsInt()),
            GrobValueKind.Float => x.AsFloat().CompareTo(y.AsFloat()),
            GrobValueKind.String => string.CompareOrdinal(x.AsString(), y.AsString()),
            GrobValueKind.Bool => x.AsBool().CompareTo(y.AsBool()),
            GrobValueKind.Struct => CompareStruct(x.AsStruct(), y.AsStruct()),
            _ => throw new NativeFaultException(RuntimeErrorLeaf, ErrorCatalog.E5906.Code,
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
        throw new NativeFaultException(RuntimeErrorLeaf, ErrorCatalog.E5906.Code,
            $"sort key type {a.TypeName} does not implement Comparable");
    }
}
