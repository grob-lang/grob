using System.Collections;

namespace Grob.Core;

/// <summary>
/// Collects <see cref="Diagnostic"/>s produced during compilation or execution.
/// Not thread-safe.
/// </summary>
public sealed class DiagnosticBag : IEnumerable<Diagnostic> {
    private readonly List<Diagnostic> _diagnostics = [];

    /// <summary>All diagnostics in insertion order.</summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>All diagnostics with <see cref="Severity.Error"/>.</summary>
    public IEnumerable<Diagnostic> Errors => _diagnostics.Where(d => d.Severity == Severity.Error);

    /// <summary>All diagnostics with <see cref="Severity.Warning"/>.</summary>
    public IEnumerable<Diagnostic> Warnings => _diagnostics.Where(d => d.Severity == Severity.Warning);

    /// <summary>All diagnostics with <see cref="Severity.Info"/>.</summary>
    public IEnumerable<Diagnostic> Infos => _diagnostics.Where(d => d.Severity == Severity.Info);

    /// <summary>All diagnostics with <see cref="Severity.Hint"/>.</summary>
    public IEnumerable<Diagnostic> Hints => _diagnostics.Where(d => d.Severity == Severity.Hint);

    /// <summary>True if any diagnostic has <see cref="Severity.Error"/>.</summary>
    public bool HasErrors => _diagnostics.Any(d => d.Severity == Severity.Error);

    /// <summary>True if any diagnostic has <see cref="Severity.Warning"/>.</summary>
    public bool HasWarnings => _diagnostics.Any(d => d.Severity == Severity.Warning);

    /// <summary>Total number of diagnostics.</summary>
    public int Count => _diagnostics.Count;

    /// <summary>
    /// Appends a <see cref="Diagnostic"/> to the bag.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to add. Must not be null.</param>
    public void Add(Diagnostic diagnostic) {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Add(diagnostic);
    }

    /// <inheritdoc/>
    public IEnumerator<Diagnostic> GetEnumerator() => _diagnostics.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _diagnostics.GetEnumerator();
}
