namespace Grob.Core;

/// <summary>
/// Thrown when a <see cref="GrobValue"/> strict accessor is called with the wrong kind.
/// This is a compiler bug, not a user bug — user scripts cannot reach this path in
/// correctly compiled code.
/// </summary>
public sealed class GrobInternalException : Exception {
    /// <summary>
    /// Initialises a new <see cref="GrobInternalException"/> with the supplied
    /// <paramref name="message"/> describing the kind mismatch.
    /// </summary>
    public GrobInternalException(string message) : base(message) { }
}
