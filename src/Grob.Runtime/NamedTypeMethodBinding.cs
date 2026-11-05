using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// A method of a plugin-owned named type: its signature (a bare name such as
/// <c>addDays</c>) and the binder the VM's <c>GetProperty</c> dispatch calls to close
/// the method over its receiver struct (D-356, moved into the plugins by D-426
/// Decision 3).
/// </summary>
public sealed record NamedTypeMethodBinding(FunctionSignature Signature, Func<GrobStruct, NativeFunction> Bind);
