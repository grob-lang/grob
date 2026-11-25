using System.Text.RegularExpressions;

namespace Grob.Torture.Campaign;

/// <summary>
/// The black-box confirmation (gate report §4.3): each minimised reproducer is run through the
/// real CLI, which shows whether a user would meet the finding as E9001 (D-451 Decision 2) or as
/// the crash the strategy's §1 forbids, a .NET stack frame or the unhandled-exception banner on
/// stderr. A stack overflow (R-70) cannot be caught, so it still shows its frames.
/// </summary>
internal static partial class BlackBox {
    /// <summary>What stderr shows.</summary>
    public static BlackBoxVerdict Classify(GrobOutput output) {
        if (output.TimedOut) return BlackBoxVerdict.TimedOut;
        if (StackFrame().IsMatch(output.Stderr) || output.Stderr.Contains("Unhandled exception", StringComparison.Ordinal)) {
            return BlackBoxVerdict.StackTrace;
        }
        return output.Stderr.Contains("error[E9001]", StringComparison.Ordinal) ? BlackBoxVerdict.Reported : BlackBoxVerdict.NotReproduced;
    }

    // The strategy's §1 frame line: "   at Namespace.Type.Method(".
    [GeneratedRegex(@"^\s+at [^\s(]+\(", RegexOptions.Multiline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StackFrame();
}
