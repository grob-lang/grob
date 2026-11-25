using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Grob.Torture.Campaign;

/// <summary>The <c>campaign</c> command's options.</summary>
/// <param name="Out">The campaign's directory.</param>
/// <param name="Programs">How many programs the plan holds, at least one.</param>
/// <param name="Seed">The campaign seed.</param>
/// <param name="Workers">How many worker processes, at least one.</param>
/// <param name="Grob">The CLI to run the black-box check through, or <see langword="null"/> to skip it.</param>
/// <param name="Root">The repository root, or <see langword="null"/> to search up from the working directory.</param>
internal sealed record CampaignOptions(string Out, int Programs, ulong Seed, int Workers, string? Grob, string? Root) {
    /// <summary>
    /// How many workers when none is named: the gate report's four, which leaves a core of a six-core
    /// machine to the supervisor and the rest of the machine (§1.5).
    /// </summary>
    public const int DefaultWorkers = 4;

    /// <summary>Reads the options of <c>campaign</c> from <paramref name="args"/>, the command word first.</summary>
    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out CampaignOptions? options) {
        options = null;
        if (!CommandOptions.TryRead(args, ["--out", "--programs", "--seed"], ["--workers", "--grob", "--root"], out Dictionary<string, string>? values)) {
            return false;
        }
        string workers = values.GetValueOrDefault("--workers", DefaultWorkers.ToString(CultureInfo.InvariantCulture));
        if (!PositiveInt(values["--programs"], out int programs) || !PositiveInt(workers, out int workerCount)
                || !ulong.TryParse(values["--seed"], NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed)) {
            return false;
        }
        options = new CampaignOptions(values["--out"], programs, seed, workerCount, values.GetValueOrDefault("--grob"), values.GetValueOrDefault("--root"));
        return true;
    }

    private static bool PositiveInt(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0;
}
