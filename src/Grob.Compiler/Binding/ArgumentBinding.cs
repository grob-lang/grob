namespace Grob.Compiler.Binding;

/// <summary>
/// The result of binding a call's arguments to a parameter list.
/// </summary>
/// <param name="SlotArgument">
/// One entry per parameter, in parameter order: the index of the argument bound to it,
/// or <see cref="ArgumentBinder.Unbound"/> when nothing was.
/// </param>
/// <param name="PositionalCount">
/// The number of positional arguments before the first named one. It can exceed the
/// parameter count; the extra positionals bind to no slot.
/// </param>
/// <param name="MissingRequired">Whether a parameter with no default was left unbound.</param>
/// <param name="Problems">Every break of the convention, in argument order.</param>
internal record ArgumentBinding(
    IReadOnlyList<int> SlotArgument,
    int PositionalCount,
    bool MissingRequired,
    IReadOnlyList<BindingProblem> Problems) {
    /// <summary>Whether more positional arguments were passed than there are parameters.</summary>
    public bool TooManyPositionals => PositionalCount > SlotArgument.Count;
}
