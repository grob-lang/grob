namespace Grob.Core;

/// <summary>
/// A callback value passed to a <see cref="NativeFunction"/>'s implementation so
/// the native can invoke a Grob callable (typically a lambda argument) back
/// through the VM's dispatch loop. <see cref="Invoke"/> hides all re-entrant
/// execution machinery from the native: the implementation calls it exactly as
/// it would call a plain C# method and receives the return value.
/// </summary>
/// <remarks>
/// A <see langword="readonly struct"/> (D-397, D-393 Q1) rather than a delegate —
/// the delegate shape required capturing <c>line</c>, <c>column</c>, a
/// <c>CancellationToken</c>, the enclosing dispatch's <c>FinallyContext</c> and
/// the VM instance into a Roslyn display class on every native call, allocating
/// both the display class and the delegate object even though 95 of 99 Stdlib
/// natives never read the parameter. As a struct, each construction site
/// (<c>VirtualMachine</c>'s <c>Call</c> handler and <c>InvokeCallable</c>'s
/// nested-native path) is a plain value, not a heap allocation.
///
/// Carries <see cref="IVmCallHost"/> rather than a concrete VM reference —
/// <c>Grob.Core</c> cannot name <c>Grob.Vm.VirtualMachine</c> without inverting
/// the DAG (<c>Grob.Vm</c> already references <c>Grob.Core</c>). No equality,
/// deconstruction or <c>ToString</c> is needed, so this is a plain
/// <see langword="readonly struct"/> rather than a <c>readonly record struct</c>.
/// </remarks>
public readonly struct VmInvoker {
    private readonly IVmCallHost _host;
    private readonly int _line;
    private readonly int _column;
    private readonly CancellationToken _cancellationToken;
    private readonly VmFinallyWindow _finallyWindow;

    /// <summary>
    /// Constructs a <see cref="VmInvoker"/> bound to <paramref name="host"/> and
    /// the calling dispatch's context. Internal — only <c>Grob.Vm</c> (granted
    /// access via <c>InternalsVisibleTo</c>) constructs one, at the two sites
    /// that previously built the delegate closure.
    /// </summary>
    internal VmInvoker(IVmCallHost host, int line, int column,
            CancellationToken cancellationToken, VmFinallyWindow finallyWindow) {
        _host = host;
        _line = line;
        _column = column;
        _cancellationToken = cancellationToken;
        _finallyWindow = finallyWindow;
    }

    /// <summary>
    /// Invokes <paramref name="callable"/> (must be a
    /// <see cref="GrobValueKind.Function"/>) with <paramref name="args"/> and
    /// returns its result, with the same re-entrancy semantics, source-position
    /// attribution, cancellation and <c>finally</c>-boundary propagation as the
    /// call site that constructed this <see cref="VmInvoker"/>.
    /// </summary>
    /// <param name="callable">
    /// The Grob function value to invoke — must be a <see cref="GrobValueKind.Function"/>.
    /// </param>
    /// <param name="args">The argument values to pass to the callable.</param>
    /// <returns>The value returned by the callable.</returns>
    public GrobValue Invoke(GrobValue callable, GrobValue[] args) =>
        _host.InvokeCallable(callable, args, _line, _column, _cancellationToken, _finallyWindow);
}
