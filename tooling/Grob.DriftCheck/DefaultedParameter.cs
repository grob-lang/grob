namespace Grob.DriftCheck;

/// <summary>
/// A defaulted parameter of a registered callable, as the code holds it (R-55).
/// </summary>
/// <param name="Callable">
/// The callable's name as the documents write it: qualified for a namespace function
/// (<c>date.parse</c>), bare for a method or a built-in (<c>padLeft</c>, <c>input</c>).
/// </param>
/// <param name="Name">The parameter's name.</param>
/// <param name="Default">
/// The default rendered as the documents write it (<c>""</c>, <c>5</c>), or
/// <see langword="null"/> when the code records the parameter as defaulted without
/// holding the value (array <c>sort</c>'s <c>descending</c>).
/// </param>
public sealed record DefaultedParameter(string Callable, string Name, string? Default);
