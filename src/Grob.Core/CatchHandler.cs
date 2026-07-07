namespace Grob.Core;

/// <summary>
/// One <c>catch</c> handler within a <see cref="TryRegion"/> (Sprint 7 Increment B).
/// </summary>
/// <param name="MatchTypeNames">
/// The concrete <c>GrobError</c>-hierarchy type names this handler matches,
/// resolved by the compiler over the subtype relationship at compile time
/// (D-274). Empty and ignored when <paramref name="IsCatchAll"/> is <see langword="true"/>.
/// </param>
/// <param name="IsCatchAll">
/// <see langword="true"/> for the bare <c>catch e { }</c> form, which matches any
/// thrown value regardless of <see cref="MatchTypeNames"/>.
/// </param>
/// <param name="HandlerOffset">Bytecode offset of the handler body's first instruction.</param>
/// <param name="BindingSlot">
/// The stack slot (relative to the frame's stack base) the thrown value is written
/// to before control transfers to <see cref="HandlerOffset"/>.
/// </param>
public sealed record CatchHandler(
    IReadOnlyList<string> MatchTypeNames,
    bool IsCatchAll,
    int HandlerOffset,
    int BindingSlot);
