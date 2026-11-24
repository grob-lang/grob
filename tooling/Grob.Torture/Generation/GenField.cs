namespace Grob.Torture.Generation;

/// <summary>A field of a struct type the generator declares, or of an anonymous struct it builds.</summary>
/// <param name="Name">The field's name.</param>
/// <param name="Type">The field's type.</param>
internal sealed record GenField(string Name, GenType Type);
