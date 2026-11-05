using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// A method on a primitive receiver (<c>s.trim()</c>) in the
/// <see cref="NativeCatalogue"/>. The signature's name is bare (<c>trim</c>);
/// <see cref="QualifiedNativeName"/> is the runtime global the compiler's
/// receiver-as-first-argument rewrite calls (<c>string.trim</c>).
/// </summary>
public sealed record PrimitiveMethodEntry(FunctionSignature Signature, string QualifiedNativeName, NativeFunction Implementation);
