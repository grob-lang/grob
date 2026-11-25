namespace Grob.Torture.Campaign;

/// <summary>
/// Starts one campaign worker over one shard and waits for it to end. The supervisor's seam: the
/// real launcher starts a child process, and the tests run the worker in process or pretend it died.
/// </summary>
internal interface IWorkerLauncher {
    /// <summary>Runs the worker for <paramref name="shard"/> of the campaign in <paramref name="directory"/>.</summary>
    /// <returns>The worker's exit code.</returns>
    int Run(string directory, int shard);
}
