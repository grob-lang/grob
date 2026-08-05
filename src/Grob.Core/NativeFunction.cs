namespace Grob.Core;

/// <summary>
/// A Grob callable whose body is a C# delegate rather than bytecode.  The VM
/// dispatches <see cref="NativeFunction"/> transparently alongside
/// <see cref="BytecodeFunction"/> — callers use the same call syntax and the VM
/// chooses the right arm.
/// </summary>
/// <remarks>
/// Lives in <c>Grob.Core</c> — the only assembly both <c>Grob.Compiler</c> (which
/// validates call signatures at compile time) and <c>Grob.Vm</c> (which dispatches
/// it at runtime) reference.
/// </remarks>
public sealed class NativeFunction : GrobFunction {
    /// <summary>
    /// The C# implementation of this native.  Receives the positional argument
    /// values and a <see cref="VmInvoker"/> value (a <see langword="readonly struct"/>
    /// since D-397, no longer a delegate). The implementation calls
    /// <see cref="VmInvoker.Invoke"/> on it to call any Grob callable (e.g. a lambda
    /// argument) back through the VM — <c>invoker(...)</c> delegate-invocation syntax
    /// no longer compiles.
    /// </summary>
    public Func<GrobValue[], VmInvoker, GrobValue> Implementation { get; }

    /// <summary>
    /// Initialises a new <see cref="NativeFunction"/> with the given
    /// <paramref name="name"/>, <paramref name="arity"/> and C#
    /// <paramref name="implementation"/>.
    /// </summary>
    public NativeFunction(string name, int arity, Func<GrobValue[], VmInvoker, GrobValue> implementation)
        : base(name, arity) {
        ArgumentNullException.ThrowIfNull(implementation);
        Implementation = implementation;
    }
}
