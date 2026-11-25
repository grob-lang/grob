namespace Grob.Torture.Campaign;

/// <summary>
/// The test seams of a <see cref="CampaignWorker"/>: a hook that blocks makes a run stall, and the
/// observer shows what the journal held, without depending on an open defect.
/// </summary>
/// <param name="Phase">Called with the program as each of its phases starts, after the journal is written.</param>
/// <param name="Journal">Called with each line written to the journal, and with an empty line when it is cleared.</param>
/// <param name="Minimise">Called as each minimising candidate's phase starts.</param>
internal sealed record WorkerHooks(
    Action<string, PipelinePhase>? Phase = null,
    Action<string>? Journal = null,
    Action<PipelinePhase>? Minimise = null);
