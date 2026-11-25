namespace Grob.Torture.Campaign;

/// <summary>
/// The processes a campaign starts: its workers and the real CLI for the black-box check. The
/// command line's seam, so that its tests run the workers in process and never start the CLI.
/// </summary>
/// <param name="Launcher">Makes the launcher for a repository root's workers.</param>
/// <param name="Grob">Makes the runner for the CLI at a path.</param>
internal sealed record CampaignServices(Func<string, IWorkerLauncher> Launcher, Func<string, IGrobRunner> Grob);
