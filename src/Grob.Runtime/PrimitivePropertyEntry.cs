using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// A property on a primitive receiver (<c>s.length</c>) in the
/// <see cref="NativeCatalogue"/>. <see cref="QualifiedNativeName"/> is the runtime
/// global the compiler's receiver-as-first-argument rewrite calls
/// (<c>string.length</c>).
/// </summary>
public sealed record PrimitivePropertyEntry(PropertySignature Signature, string QualifiedNativeName, NativeFunction Implementation);
