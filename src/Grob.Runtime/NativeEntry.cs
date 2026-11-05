using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// A namespace native in the <see cref="NativeCatalogue"/>: its signature, the
/// implementation the VM binds and the name of the plugin that registered it (D-426
/// Decision 2). The signature's name is qualified (<c>math.sqrt</c>).
/// </summary>
public sealed record NativeEntry(FunctionSignature Signature, NativeFunction Implementation, string Owner);
