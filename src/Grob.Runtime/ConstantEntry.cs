using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// A namespace constant in the <see cref="NativeCatalogue"/> (<c>math.pi</c>,
/// <c>guid.empty</c>): its qualified name, the compile-time type derived from the value's
/// kind, the value itself and the owning plugin.
/// </summary>
public sealed record ConstantEntry(string Name, SignatureType.Concrete Type, GrobValue Value, string Owner);
