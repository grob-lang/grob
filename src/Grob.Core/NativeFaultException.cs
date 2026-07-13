namespace Grob.Core;

/// <summary>
/// Thrown by a <see cref="NativeFunction"/>'s C# <c>Implementation</c> delegate to signal
/// a catchable Grob domain error (Sprint 8 Increment A — the native-throw seam, D-342).
/// The VM's <c>Call</c> dispatch catches this and routes it through the same
/// handler-table walk a VM-detected runtime fault already uses (<c>VirtualMachine
/// .TryRaiseRuntimeGrobError</c>, D-334) — the same mechanism a user <c>throw</c> and an
/// int-division-by-zero fault both go through. <see cref="LeafTypeName"/> must name one of
/// the ten <c>GrobError</c> leaves (e.g. <c>"ArithmeticError"</c>); the runtime does not
/// validate this against the compile-time hierarchy (<c>Grob.Core</c> has no reference to
/// <c>Grob.Compiler</c>, where the hierarchy is registered) — the native author is trusted
/// to pass a real leaf name, exactly as every existing VM-internal fault site already does.
/// <see cref="Code"/> carries the specific <c>ErrorCatalog</c> code for the *unhandled*
/// top-level diagnostic (e.g. <c>ErrorCatalog.E5006.Code</c> for a <c>math</c> domain
/// violation) — each native supplies its own registered code at its throw site, exactly as
/// every existing VM-internal fault site's C# exception constructor already takes a code
/// parameter rather than the VM inferring one.
/// </summary>
public sealed class NativeFaultException : Exception {
    /// <summary>The <c>GrobError</c> leaf to construct, e.g. <c>"ArithmeticError"</c>.</summary>
    public string LeafTypeName { get; }

    /// <summary>The registered <c>ErrorCatalog</c> code for the unhandled top-level diagnostic.</summary>
    public string Code { get; }

    /// <summary>
    /// Initialises a new <see cref="NativeFaultException"/> naming the
    /// <paramref name="leafTypeName"/> leaf to raise and the <paramref name="code"/> to use
    /// if it goes unhandled, with the given <paramref name="message"/> stored on the
    /// constructed error's <c>message</c> field.
    /// </summary>
    public NativeFaultException(string leafTypeName, string code, string message) : base(message) {
        ArgumentException.ThrowIfNullOrEmpty(leafTypeName);
        ArgumentException.ThrowIfNullOrEmpty(code);
        LeafTypeName = leafTypeName;
        Code = code;
    }
}
