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
public sealed record TryRegion(int StartOffset, int EndOffset, IReadOnlyList<CatchHandler> Handlers);
