namespace Grob.Core;

/// <summary>
/// A callback delegate passed to a <see cref="NativeFunction"/>'s implementation so
/// the native can invoke a Grob callable (typically a lambda argument) back through
/// the VM's dispatch loop.  The delegate hides all re-entrant execution machinery
/// from the native: the implementation calls it exactly as it would call a plain C#
/// function and receives the return value.
/// </summary>
/// <param name="callable">
/// The Grob function value to invoke — must be a <see cref="GrobValueKind.Function"/>.
/// </param>
/// <param name="args">The argument values to pass to the callable.</param>
/// <returns>The value returned by the callable.</returns>
public delegate GrobValue VmInvoker(GrobValue callable, GrobValue[] args);
