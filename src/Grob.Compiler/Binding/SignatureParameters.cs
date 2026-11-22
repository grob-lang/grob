using Grob.Runtime;

namespace Grob.Compiler.Binding;

/// <summary>
/// A native's parameter list as the binder sees it, read from its registered
/// <see cref="FunctionSignature"/>. A variadic tail is one parameter with no default
/// (<see cref="SignatureParameter"/> refuses a default on it), so positionals past the
/// fixed prefix fill it, naming it is E0009 and any other name is E0011: D-446
/// Decision 4 needs no rule of its own here.
/// </summary>
internal readonly struct SignatureParameters(FunctionSignature signature) : IBindableParameters {
    /// <inheritdoc/>
    public int Count => signature.Parameters.Count;

    /// <inheritdoc/>
    public string NameAt(int index) => signature.Parameters[index].Name;

    /// <inheritdoc/>
    public bool HasDefaultAt(int index) => signature.Parameters[index].HasDefault;
}
