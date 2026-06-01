using Grob.Core;

namespace Grob.Cli;

/// <summary>
/// Formats <see cref="Diagnostic"/>s for stderr output using the Grob
/// diagnostic style (personality doc: "error[Exxxx]: message\n  --> file:line:col").
/// </summary>
internal static class DiagnosticFormatter {
    /// <summary>
    /// Writes all diagnostics in <paramref name="bag"/> to <paramref name="writer"/>.
    /// </summary>
    internal static void Write(DiagnosticBag bag, TextWriter writer) {
        foreach (Diagnostic d in bag.Diagnostics) {
            Write(d, writer);
        }
    }

    /// <summary>
    /// Writes a single <paramref name="diagnostic"/> to <paramref name="writer"/>.
    /// </summary>
    internal static void Write(Diagnostic diagnostic, TextWriter writer) {
        string label = diagnostic.Severity == Severity.Warning ? "warning" : "error";
        writer.WriteLine($"{label}[{diagnostic.Code}]: {diagnostic.Message}");
        writer.WriteLine($"  --> {diagnostic.Range}");
    }

    /// <summary>
    /// Writes a <see cref="GrobRuntimeException"/> as a runtime diagnostic to
    /// <paramref name="writer"/>, using <paramref name="file"/> as the source file path.
    /// </summary>
    internal static void WriteRuntime(GrobRuntimeException ex, string file, TextWriter writer) {
        string location = ex.Column > 0
            ? $"{file}:{ex.Line}:{ex.Column}"
            : $"{file}:{ex.Line}";
        writer.WriteLine($"error[{ex.Code}]: {ex.Message}");
        writer.WriteLine($"  --> {location}");
    }
}
