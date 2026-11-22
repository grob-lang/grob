using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// One parameter's slot in a bound call: the argument of the call bound to it, or the
/// parameter's default.
/// </summary>
/// <param name="ArgumentIndex">
/// The index, in source order, of the argument bound to this parameter, or
/// <see cref="DefaultArgument"/> when the parameter takes its default.
/// </param>
/// <param name="NativeDefault">
/// A native parameter's default, the compile-time constant on its signature (D-358),
/// emitted as one constant. <see langword="null"/> for an argument slot and for a script
/// function's default, which is an expression the compiler emits from the declaration.
/// </param>
public readonly record struct BoundSlot(int ArgumentIndex, GrobValue? NativeDefault) {
    /// <summary>The <see cref="ArgumentIndex"/> of a slot that takes its default.</summary>
    public const int DefaultArgument = -1;

    /// <summary>Whether this slot takes the parameter's default.</summary>
    public bool IsDefault => ArgumentIndex == DefaultArgument;
}
