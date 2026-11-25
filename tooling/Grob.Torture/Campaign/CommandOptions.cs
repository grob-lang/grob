using System.Diagnostics.CodeAnalysis;

namespace Grob.Torture.Campaign;

/// <summary>
/// Reads a command's <c>--name value</c> pairs: every name known, none twice, each with a value.
/// </summary>
internal static class CommandOptions {
    /// <summary>
    /// The pairs after the command word in <paramref name="args"/>, or <see langword="false"/> when
    /// a name is unknown, repeated or has no value, or a required name is missing.
    /// </summary>
    public static bool TryRead(IReadOnlyList<string> args, IReadOnlyCollection<string> required, IReadOnlyCollection<string> optional,
            [NotNullWhen(true)] out Dictionary<string, string>? values) {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Count; i += 2) {
            string name = args[i];
            bool known = required.Contains(name) || optional.Contains(name);
            if (!known || i + 1 >= args.Count || !values.TryAdd(name, args[i + 1])) {
                values = null;
                return false;
            }
        }
        if (required.All(values.ContainsKey)) return true;
        values = null;
        return false;
    }
}
