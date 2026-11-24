namespace Grob.Torture.Generation;

/// <summary>A binding the generator has declared, with whether a program may assign it.</summary>
/// <param name="Name">The binding's name, unique within its program.</param>
/// <param name="Type">The binding's type.</param>
/// <param name="Assignable">False for a parameter, a loop variable, a loop counter, <c>const</c> and <c>readonly</c>.</param>
internal sealed record GenVariable(string Name, GenType Type, bool Assignable);
