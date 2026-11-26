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

    /// <summary>
    /// Initialises a new <see cref="GrobInternalException"/> that reports a host exception it
    /// wraps (D-459): the message names what failed, and the host exception stays reachable as
    /// <see cref="Exception.InnerException"/> for <c>--verbose</c>.
    /// </summary>
    public GrobInternalException(string message, Exception innerException) : base(message, innerException) { }
}
