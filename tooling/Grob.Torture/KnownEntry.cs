namespace Grob.Torture;

/// <summary>One entry in the known-findings list.</summary>
/// <param name="Row">The open register row, <c>R-##</c>, that the finding is filed as.</param>
/// <param name="Signature">The signature's <see cref="Torture.Signature.Key"/>.</param>
/// <param name="Programs">The program ids the entry covers, and the only ones it suppresses.</param>
internal sealed record KnownEntry(string Row, string Signature, IReadOnlyList<string> Programs);
