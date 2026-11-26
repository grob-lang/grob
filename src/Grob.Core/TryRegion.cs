namespace Grob.Core;

/// <summary>
/// A protected <c>try</c> region within a <see cref="Chunk"/> (Sprint 7 Increment B).
/// Recorded by the compiler when it emits <see cref="OpCode.TryBegin"/>/<see cref="OpCode.TryEnd"/>;
/// consulted by the VM's <see cref="OpCode.Throw"/> arm to find the nearest matching handler.
/// </summary>
/// <param name="StartOffset">
/// Bytecode offset of the first instruction of the protected try body (just past
/// <see cref="OpCode.TryBegin"/>'s operand).
/// </param>
/// <param name="EndOffset">
/// Bytecode offset just past the try body's last instruction — <em>excludes</em> the
/// catch bodies, so a throw from inside a handler is not re-caught by the same region.
/// </param>
/// <param name="Handlers">The region's catch handlers, in source order.</param>
/// <param name="FinallyOffset">
/// Bytecode offset of the region's compiled <c>finally</c> body (Sprint 7 Increment C,
/// D-275), or <c>-1</c> when the <c>try</c> has no <c>finally</c> — the codebase's
/// established "absent" sentinel for a small-int table field (see
/// <c>Compiler.FindLocalSlot</c>/<c>ResolveUpvalue</c>).
/// </param>
/// <param name="BaseSlot">
/// The number of stack slots, relative to the frame's stack base, live when the <c>try</c>
/// begins: the slot its first local, a catch binding or the finally's first local takes.
/// On the exceptional path the VM trims the stack to it before running the finally, so
/// the finally finds the stack as the normal path leaves it (D-455). Required, with no
/// default, since a wrong base silently misplaces the finally's locals.
/// </param>
/// <param name="TryEndOffset">
/// Bytecode offset of the region's closing <see cref="OpCode.TryEnd"/>, after the catch
/// bodies and the finally. A finally that a throw runs ends when that instruction executes
/// in the finally's own frame (D-457): a try nested inside the finally can be left without
/// reaching its own <see cref="OpCode.TryEnd"/>, by a throw caught further out, a
/// <c>break</c>, a <c>continue</c> or a callee's <c>return</c>, so no count of the markers
/// executed can find the end. Required, with no default, since a wrong end runs the
/// finally past its body or stops it short.
/// </param>
public sealed record TryRegion(
    int StartOffset, int EndOffset, IReadOnlyList<CatchHandler> Handlers, int FinallyOffset, int BaseSlot,
    int TryEndOffset);
