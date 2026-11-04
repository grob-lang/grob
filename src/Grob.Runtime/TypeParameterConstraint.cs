namespace Grob.Runtime;

/// <summary>
/// The constraint a <see cref="TypeParameter"/> places on its argument (D-418). The two
/// v1 shapes are flags so a signature can accept either: <c>json.Node.mapAs&lt;T&gt;</c>
/// takes a named struct or an array of one, <c>csv.Table.mapAs&lt;T&gt;</c> only an
/// array of one. Checking the constraint is the <c>mapAs&lt;T&gt;</c> increment's job;
/// adding a flag later is non-breaking.
/// </summary>
[Flags]
public enum TypeParameterConstraint {
    /// <summary>Any type argument.</summary>
    None = 0,

    /// <summary>The argument must be a named struct type declared with <c>type</c>.</summary>
    NamedStruct = 1,

    /// <summary>The argument must be an array of a named struct type.</summary>
    ArrayOfNamedStruct = 2,
}
