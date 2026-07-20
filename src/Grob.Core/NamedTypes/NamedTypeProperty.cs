namespace Grob.Core.NamedTypes;

/// <summary>
/// One instance property on a <see cref="NamedTypeEntry"/> — the declared return
/// <see cref="GrobType"/> and the runtime accessor VM dispatch binds directly.
/// </summary>
/// <param name="Name">The property name as written in Grob source (e.g. <c>version</c>).</param>
/// <param name="Type">The property's declared type.</param>
/// <param name="Get">Reads the property's value from a receiver of this entry's nominal type.</param>
public sealed record NamedTypeProperty(string Name, GrobType Type, Func<GrobStruct, GrobValue> Get);
