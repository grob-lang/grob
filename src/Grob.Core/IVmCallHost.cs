namespace Grob.Core;

/// <summary>
/// The re-entrant call-back seam <see cref="VmInvoker"/> holds a reference to,
/// so a <see cref="NativeFunction"/> can invoke a Grob callable (typically a
/// lambda argument) back through the VM's dispatch loop without <c>Grob.Core</c>
/// naming the concrete <c>VirtualMachine</c> type.
/// </summary>
/// <remarks>
/// Declared here — not against the concrete VM type — because the DAG forbids
/// <c>Grob.Core</c> from referencing <c>Grob.Vm</c> (<c>Grob.Vm</c> already
/// references <c>Grob.Core</c>; the reverse edge would cycle). Mirrors the
/// existing <c>IPluginRegistrar</c> precedent (<c>Grob.Runtime</c>, D-3xx Sprint
/// 8 Increment A) — a registration/call surface declared one layer below the
/// concrete VM so a lower assembly can hold a reference to it — one layer
/// further down again, since <see cref="VmInvoker"/> (and therefore this
/// interface) is needed by <c>Grob.Core</c> itself, not just by plugin authors.
/// <c>VirtualMachine</c> implements this interface explicitly in <c>Grob.Vm</c>
/// (D-397), so no new public member appears on <c>VirtualMachine</c> purely to
/// satisfy this seam.
/// </remarks>
internal interface IVmCallHost {
    /// <summary>
    /// Invokes <paramref name="callable"/> (typically a lambda argument received
    /// by a <see cref="NativeFunction"/>) with <paramref name="args"/> and
    /// returns its result — the re-entrant bridge <see cref="VmInvoker.Invoke"/>
    /// forwards to. <paramref name="line"/>/<paramref name="column"/> are the
    /// ORIGINATING <c>Call</c> opcode's source position (attributed to any
    /// runtime fault the invocation raises); <paramref name="finallyWindow"/>
    /// threads the enclosing dispatch's bounded-finally state so a fault can
    /// still resolve to the correct handler.
    /// </summary>
    GrobValue InvokeCallable(GrobValue callable, GrobValue[] args, int line, int column,
        CancellationToken cancellationToken, VmFinallyWindow finallyWindow);
}
