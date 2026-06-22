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
}

// ---------------------------------------------------------------------------
// Comparable key ordering for sort (D-281)
// ---------------------------------------------------------------------------

/// <summary>
/// Orders <see cref="GrobValue"/> sort keys. Supports Int (long), Float (double),
/// String (ordinal), and Bool (false &lt; true). Other kinds throw
/// <see cref="GrobRuntimeException"/>; the type checker defers Comparable
/// validation to Sprint 5 Increment D.
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
            _ => throw new GrobRuntimeException(
                     ErrorCatalog.E0004.Code, UnknownLine, UnknownColumn,
                     $"sort key type {x.Kind} does not implement Comparable"),
        };
    }
}
