namespace Grob.Core;

/// <summary>
/// The Grob type system. Values are assigned by the type checker; Sprint 1 code
/// always sees <see cref="Unknown"/> — the concrete members land in Sprint 2.
/// </summary>
public enum GrobType {
    /// <summary>
    /// Not yet resolved. The default value for any <see cref="GrobType"/> field
    /// before the type checker (Sprint 2) runs.
    /// </summary>
    Unknown = 0,
}
