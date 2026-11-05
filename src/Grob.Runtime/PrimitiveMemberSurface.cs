using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The instance members registered on one primitive receiver (<c>int</c>, <c>float</c>,
/// <c>string</c> or <c>bool</c>), keyed by bare member name. Only first-party code can
/// populate one (D-426 Decision 3); every plugin can read one.
/// </summary>
public sealed record PrimitiveMemberSurface(
    GrobType Receiver,
    IReadOnlyDictionary<string, PrimitivePropertyEntry> Properties,
    IReadOnlyDictionary<string, PrimitiveMethodEntry> Methods);
