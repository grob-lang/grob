namespace Grob.DriftCheck;

/// <summary>
/// The outcome of a single consistency check. A check is green only when it
/// actually compared two live facts and found them in agreement — see
/// <see cref="CheckResult.Pass"/>. A check that found nothing to compare is a
/// failure, not a pass (see <see cref="AnchorNotFoundException"/>): a green
/// result must mean "checked and agreed", never "found nothing to check".
/// </summary>
/// <param name="Name">Short, stable name of the check (e.g. "error-code count").</param>
/// <param name="Ok"><see langword="true"/> when no discrepancies were found.</param>
/// <param name="Discrepancies">
/// Human-readable discrepancy messages, each naming the document or enum and the
/// disagreement. Empty when <paramref name="Ok"/> is <see langword="true"/>.
/// </param>
public sealed record CheckResult(
    string Name,
    bool Ok,
    IReadOnlyList<string> Discrepancies) {

    /// <summary>Constructs a passing result for the named check.</summary>
    /// <param name="name">The check name.</param>
    /// <returns>A passing <see cref="CheckResult"/> with no discrepancies.</returns>
    public static CheckResult Pass(string name) => new(name, true, []);

    /// <summary>Constructs a failing result carrying the discrepancy messages.</summary>
    /// <param name="name">The check name.</param>
    /// <param name="discrepancies">The discrepancies found; must be non-empty.</param>
    /// <returns>A failing <see cref="CheckResult"/>.</returns>
    public static CheckResult Fail(string name, IReadOnlyList<string> discrepancies)
        => new(name, false, discrepancies);

    /// <summary>
    /// A single-line summary suitable for console output: the verdict, the check
    /// name and, when failing, the discrepancy list.
    /// </summary>
    /// <returns>The formatted summary line.</returns>
    public string Summarise() {
        if (Ok) return $"PASS  {Name}";
        var joined = string.Join($"{Environment.NewLine}        ", Discrepancies);
        return $"FAIL  {Name}{Environment.NewLine}        {joined}";
    }
}

/// <summary>
/// Thrown when a defensive parser cannot locate the section it must read. The
/// drift gate treats a missing anchor as a hard failure so a corpus edit that
/// renames or removes an anchor can never make a check pass silently by finding
/// nothing.
/// </summary>
public sealed class AnchorNotFoundException : Exception {
    /// <summary>Constructs the exception naming the document and the expected anchor.</summary>
    /// <param name="document">The document the parser was reading.</param>
    /// <param name="anchor">The section anchor or pattern that was expected but not found.</param>
    public AnchorNotFoundException(string document, string anchor)
        : base($"Consistency gate: could not locate '{anchor}' in '{document}'. " +
               "The anchor was renamed, removed or the document moved; a check cannot " +
               "pass by finding nothing. Restore the anchor or update the gate.") {
        Document = document;
        Anchor = anchor;
    }

    /// <summary>The document the parser was reading when the anchor was missing.</summary>
    public string Document { get; }

    /// <summary>The section anchor or pattern that was expected.</summary>
    public string Anchor { get; }
}
