using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// A property of a plugin-owned named type: its signature and the getter the VM's
/// <c>GetProperty</c> dispatch calls on the receiver struct (D-356, moved into the
/// plugins by D-426 Decision 3).
/// </summary>
public sealed record NamedTypePropertyBinding(PropertySignature Signature, Func<GrobStruct, GrobValue> Get);
