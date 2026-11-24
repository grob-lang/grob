namespace Grob.Torture.Campaign;

/// <summary>Runs a script through the real CLI: the black-box check's seam.</summary>
internal interface IGrobRunner {
    /// <summary>Runs <c>grob run <paramref name="scriptPath"/></c> and waits for it.</summary>
    GrobOutput Run(string scriptPath);
}
