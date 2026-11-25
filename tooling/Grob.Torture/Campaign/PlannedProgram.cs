namespace Grob.Torture.Campaign;

/// <summary>One program of a campaign's plan, named but not yet written.</summary>
/// <param name="Id">
/// The program's id: <c>generated/&lt;campaign-seed&gt;/&lt;index&gt;</c> for a generated program and
/// <c>&lt;seed-id&gt;/&lt;operator&gt;/&lt;draw&gt;</c> for a mutant, from which its source is rebuilt.
/// </param>
/// <param name="Kind"><see cref="CampaignPlan.GeneratedKind"/>, or the name of the operator that made the mutant.</param>
internal sealed record PlannedProgram(string Id, string Kind);
