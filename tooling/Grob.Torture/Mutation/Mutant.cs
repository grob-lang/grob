namespace Grob.Torture.Mutation;

/// <summary>One mutated program and how it was made.</summary>
/// <param name="ProgramId">The program's id, <c>&lt;seed-id&gt;/&lt;operator&gt;/&lt;draw&gt;</c> (gate report §4.5).</param>
/// <param name="SeedId">The id of the seed it was made from.</param>
/// <param name="Operator">The operator's name.</param>
/// <param name="Draw">The draw number, which with the campaign seed and the operator fixes the edit.</param>
/// <param name="Source">The mutated program text.</param>
/// <param name="Edits">The edits made to the seed's text.</param>
internal sealed record Mutant(string ProgramId, string SeedId, string Operator, int Draw, string Source, IReadOnlyList<SourceEdit> Edits);
