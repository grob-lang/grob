namespace Grob.Core;

/// <summary>
/// A minimal <c>Grob.Core</c> twin of <c>VirtualMachine</c>'s own private
/// <c>FinallyContext</c> record struct (declared in <c>Grob.Vm</c>, next to
/// <c>InvokeCallable</c>), carrying only the three fields <see cref="VmInvoker"/>
/// needs to thread through <c>IVmCallHost.InvokeCallable</c> across the
/// <c>Grob.Core</c>/<c>Grob.Vm</c> assembly boundary (D-397).
/// </summary>
/// <remarks>
/// <c>FinallyContext</c> itself stays exactly where it is, private to
/// <c>VirtualMachine</c> — it is VM-internal bookkeeping for
/// <c>TryRaiseRuntimeGrobError</c>'s bounded-finally handling, not a shape the
/// compiler or any other <c>Grob.Core</c> consumer needs to see. Promoting it
/// wholesale into <c>Grob.Core</c> was considered and rejected as unnecessary
/// surface growth; this twin exists purely so <see cref="VmInvoker"/> — which
/// DOES need to live in <c>Grob.Core</c>, next to <c>NativeFunction</c> — has
/// something to carry the same three values in. <c>VirtualMachine</c>'s
/// explicit <c>IVmCallHost.InvokeCallable</c> implementation converts between
/// the two record structs at the single crossing point.
/// </remarks>
/// <param name="Bounded">
/// <see langword="true"/> when the enclosing dispatch is running a single
/// <c>finally</c> body on the exceptional unwind path (mirrors
/// <c>FinallyContext.Bounded</c>).
/// </param>
/// <param name="BoundaryFloor">Frame count the bounded finally runs at, or −1
/// (mirrors <c>FinallyContext.BoundaryFloor</c>).</param>
/// <param name="BoundaryStart">Start offset of the bounded finally body, or −1
/// (mirrors <c>FinallyContext.BoundaryStart</c>).</param>
internal readonly record struct VmFinallyWindow(bool Bounded, int BoundaryFloor, int BoundaryStart);
