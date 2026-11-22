namespace Grob.Compiler.Binding;

/// <summary>
/// One break of the calling convention: its kind and the index, in source order, of
/// the argument it is reported at.
/// </summary>
/// <param name="Kind">Which rule the argument breaks.</param>
/// <param name="ArgumentIndex">The argument's index in the call, in source order.</param>
internal readonly record struct BindingProblem(BindingProblemKind Kind, int ArgumentIndex);
