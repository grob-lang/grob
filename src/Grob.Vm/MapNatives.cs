using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Factory for the map method-family natives: <c>get</c> and <c>contains</c> (Sprint 9
/// Increment C0b-2a, D-377) — the non-property query members completing the map's
/// non-mutating surface (<c>length</c>/<c>isEmpty</c>/<c>keys</c>/<c>values</c> are
/// properties, resolved directly at <see cref="OpCode.GetProperty"/> dispatch time
/// alongside <c>keys</c>'s pre-existing handling). Each method is bound to its receiver
/// map at dispatch time, mirroring <see cref="ArrayNatives.GetMethod"/>. Neither member
/// takes a function argument, so — unlike <see cref="ArrayNatives.GetMethod"/> — no
/// <see cref="VmInvoker"/> callback is accepted: taking one purely for signature parity
/// would make the VM allocate a capturing delegate and a <c>FinallyContext</c> on every
/// map property dispatch for a parameter nothing reads (CodeRabbit review, PR #165).
/// </summary>
internal static class MapNatives {
    /// <summary>
    /// Returns the bound <see cref="NativeFunction"/> for the given
    /// <paramref name="methodName"/> on <paramref name="receiver"/>, or
    /// <see langword="null"/> when the name is not a recognised map method.
    /// </summary>
    internal static NativeFunction? GetMethod(string methodName, GrobMap receiver) =>
        methodName switch {
            "get" => new NativeFunction("get", 1, (args, _) => Get(args, receiver)),
            "contains" => new NativeFunction("contains", 1, (args, _) => Contains(args, receiver)),
            _ => null,
        };

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
}
