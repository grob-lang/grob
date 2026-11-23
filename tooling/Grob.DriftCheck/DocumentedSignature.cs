namespace Grob.DriftCheck;

/// <summary>A callable signature found in a design document.</summary>
/// <param name="Document">The file the signature was found in.</param>
/// <param name="Line">The 1-based line number.</param>
/// <param name="Callable">The callable's name as written: <c>regex.compile</c>, <c>padLeft</c>.</param>
/// <param name="Parameters">The parameters in the order written.</param>
public sealed record DocumentedSignature(
    string Document,
    int Line,
    string Callable,
    IReadOnlyList<DocumentedParameter> Parameters);
