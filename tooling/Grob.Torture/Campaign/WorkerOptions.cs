using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Grob.Torture.Campaign;

/// <summary>The <c>campaign-worker</c> command's options.</summary>
/// <param name="Out">The campaign's directory.</param>
/// <param name="Shard">The worker's shard, from zero.</param>
/// <param name="Root">The repository root, or <see langword="null"/> to search up from the working directory.</param>
internal sealed record WorkerOptions(string Out, int Shard, string? Root) {
    /// <summary>Reads the options of <c>campaign-worker</c> from <paramref name="args"/>, the command word first.</summary>
    public static bool TryParse(IReadOnlyList<string> args, [NotNullWhen(true)] out WorkerOptions? options) {
        options = null;
        if (!CommandOptions.TryRead(args, ["--out", "--shard"], ["--root"], out Dictionary<string, string>? values)
                || !int.TryParse(values["--shard"], NumberStyles.None, CultureInfo.InvariantCulture, out int shard)) {
            return false;
        }
        options = new WorkerOptions(values["--out"], shard, values.GetValueOrDefault("--root"));
        return true;
    }
}
