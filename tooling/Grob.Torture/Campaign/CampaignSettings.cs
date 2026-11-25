namespace Grob.Torture.Campaign;

/// <summary>
/// What a campaign was started with, kept in its directory so that its workers read one plan and a
/// restarted campaign resumes the same one.
/// </summary>
/// <param name="Seed">The campaign seed, from which every program is drawn.</param>
/// <param name="Programs">How many programs the plan holds.</param>
/// <param name="Workers">How many worker processes share the plan.</param>
internal sealed record CampaignSettings(ulong Seed, int Programs, int Workers);
