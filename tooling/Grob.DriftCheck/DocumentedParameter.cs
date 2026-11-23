namespace Grob.DriftCheck;

/// <summary>One parameter of a signature a document shows.</summary>
/// <param name="Name">The parameter's name as written.</param>
/// <param name="Default">The default as written (<c>""</c>, <c>5</c>), or <see langword="null"/> when none is shown.</param>
public sealed record DocumentedParameter(string Name, string? Default);
