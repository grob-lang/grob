using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Factory for the map method-family natives: <c>get</c>/<c>contains</c> (Sprint 9
/// Increment C0b-2a, D-377) and the in-place-mutating <c>set</c>/<c>remove</c>/<c>clear</c>
/// (Sprint 9 Increment C0b-2b, D-378), completing the map's member surface
/// (<c>length</c>/<c>isEmpty</c>/<c>keys</c>/<c>values</c> are properties, resolved
/// directly at <see cref="OpCode.GetProperty"/> dispatch time). Each method is bound to
/// its receiver map at dispatch time, mirroring <see cref="ArrayNatives.GetMethod"/>. None
/// of the five members takes a function argument, so — unlike
/// <see cref="ArrayNatives.GetMethod"/> — no <see cref="VmInvoker"/> callback is accepted:
/// taking one purely for signature parity would make the VM allocate a capturing delegate
/// and a <c>FinallyContext</c> on every map property dispatch for a parameter nothing
/// reads (CodeRabbit review, PR #165).
/// </summary>
internal static class MapNatives {
    /// <summary>
    /// Returns the bound <see cref="NativeFunction"/> for the given
    /// <paramref name="methodName"/> on <paramref name="receiver"/>, or
    /// <see langword="null"/> when the name is not a recognised map method. Consults
    /// <paramref name="receiver"/>'s per-receiver method cache first (D-393 Q2), on the
    /// same terms as <see cref="ArrayNatives.GetMethod"/>: every bound native below
    /// closes over <c>receiver</c> and nothing else, so caching is invalidation-free.
    /// </summary>
    internal static NativeFunction? GetMethod(string methodName, GrobMap receiver) {
        NativeFunction? cached = receiver.GetCachedMethod(methodName);
        if (cached is not null) return cached;

        NativeFunction? method = methodName switch {
            "get" => new NativeFunction("get", 1, (args, _) => Get(args, receiver)),
            "contains" => new NativeFunction("contains", 1, (args, _) => Contains(args, receiver)),
            // Sprint 9 Increment C0b-2b (D-378): the in-place-mutating members — none
            // take a function argument either.
            "set" => new NativeFunction("set", 2, (args, _) => Set(args, receiver)),
            "remove" => new NativeFunction("remove", 1, (args, _) => Remove(args, receiver)),
            "clear" => new NativeFunction("clear", 0, (_, _) => Clear(receiver)),
            _ => null,
        };
        if (method is not null) receiver.CacheMethod(methodName, method);
        return method;
    }

    // -----------------------------------------------------------------------
    // get(key: K) -> V? — nil when the key is absent, the same GrobMap.TryGetValue
    // lookup OpCode.GetIndex's map arm ('m[k]') already uses, so the two agree.
    // -----------------------------------------------------------------------

    private static GrobValue Get(GrobValue[] args, GrobMap receiver) =>
        receiver.TryGetValue(args[0].AsString(), out GrobValue value) ? value : GrobValue.Nil;

    // -----------------------------------------------------------------------
    // contains(key: K) -> bool — key membership (unlike the array's value-membership
    // contains(v), D-371 — a deliberate asymmetry).
    // -----------------------------------------------------------------------

    private static GrobValue Contains(GrobValue[] args, GrobMap receiver) =>
        GrobValue.FromBool(receiver.Entries.ContainsKey(args[0].AsString()));

    // -----------------------------------------------------------------------
    // set(key: K, value: V) -> void — insert or overwrite, mutating in place. Calls the
    // same GrobMap.Set OpCode.SetIndex's map arm ('m[k] = v') already uses (D-350), so
    // the two write paths can never drift.
    // -----------------------------------------------------------------------

    private static GrobValue Set(GrobValue[] args, GrobMap receiver) {
        receiver.Set(args[0].AsString(), args[1]);
        return GrobValue.Nil;
    }

    // -----------------------------------------------------------------------
    // remove(key: K) -> void — no-op if the key is absent, the OPPOSITE of the array's
    // bounds-checked, throwing remove(index) (D-373) — a deliberate asymmetry.
    // -----------------------------------------------------------------------

    private static GrobValue Remove(GrobValue[] args, GrobMap receiver) {
        receiver.Remove(args[0].AsString());
        return GrobValue.Nil;
    }

    // -----------------------------------------------------------------------
    // clear() -> void — removes all entries, mutating in place.
    // -----------------------------------------------------------------------

    private static GrobValue Clear(GrobMap receiver) {
        receiver.Clear();
        return GrobValue.Nil;
    }
}
