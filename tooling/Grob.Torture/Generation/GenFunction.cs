namespace Grob.Torture.Generation;

/// <summary>A function the program declares, with its type.</summary>
/// <param name="Name">The function's name.</param>
/// <param name="Type">Its parameter and return types.</param>
internal sealed record GenFunction(string Name, GenType.Function Type);
