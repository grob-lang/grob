using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// A property on a receiver (<c>s.length</c>, <c>d.year</c>): a bare name and a concrete
/// type, no parameters, never called with parentheses. Its own record rather than a
/// zero-parameter function signature because the distinction is data the checker reports
/// on — calling a property is E1002 today and the probe corpus pins that.
/// </summary>
public sealed record PropertySignature {
    /// <summary>
    /// Creates a property signature. Throws <see cref="ArgumentException"/> when
    /// <paramref name="name"/> is not a single identifier or <paramref name="type"/> is
    /// <c>nil</c>.
    /// </summary>
    public PropertySignature(string name, SignatureType.Concrete type) {
        Identifiers.RequireIdentifier(name, nameof(name), "A property name");
        ArgumentNullException.ThrowIfNull(type);
        if (type.Type == GrobType.Nil) {
            throw new ArgumentException("A property cannot have nil as its type.", nameof(type));
        }
        Name = name;
        Type = type;
    }

    /// <summary>The property's bare name.</summary>
    public string Name { get; }

    /// <summary>The property's type. A property has no type parameters, so it is always concrete.</summary>
    public SignatureType.Concrete Type { get; }
}
