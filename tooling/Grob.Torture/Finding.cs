namespace Grob.Torture;

/// <summary>One finding from one run.</summary>
/// <param name="Signature">The finding's signature.</param>
/// <param name="Message">What was observed, unnormalised, for the reader.</param>
/// <param name="FirstFrame">
/// For an exception, the first stack frame in <c>Grob.Compiler</c>, <c>Grob.Vm</c> or
/// <c>Grob.Stdlib</c>, as <c>Type.Method</c>; otherwise <see langword="null"/>. A triage aid,
/// not part of the signature.
/// </param>
internal sealed record Finding(Signature Signature, string Message, string? FirstFrame = null);
