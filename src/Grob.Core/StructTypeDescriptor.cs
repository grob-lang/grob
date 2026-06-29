namespace Grob.Core;

/// <summary>
/// Describes a struct type as seen by the VM's <c>NewStruct</c> handler: the type
/// name and the field names in declaration order, matching the stack layout.
/// </summary>
/// <param name="TypeName">The struct type name as declared.</param>
/// <param name="FieldNames">Field names in declaration order — one per stack slot popped.</param>
public sealed record StructTypeDescriptor(string TypeName, IReadOnlyList<string> FieldNames);
