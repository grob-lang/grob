namespace Grob.Compiler.Ast;

/// <summary>
/// A call's arguments bound to the callee's parameters, in parameter order: one
/// <see cref="BoundSlot"/> per parameter. The type checker builds it once, from the one
/// binder (D-446 Decision 1), and the compiler emits the slots in order. Because the
/// compiler reads the checker's binding instead of binding again, a call cannot be
/// checked in one order and emitted in another, which is the miscompile R-20 was opened
/// for.
/// </summary>
/// <param name="Slots">One slot per parameter, in parameter order.</param>
public sealed record BoundArgumentList(IReadOnlyList<BoundSlot> Slots);
