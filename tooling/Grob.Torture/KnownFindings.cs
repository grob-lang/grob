using System.Text.Json;

using Grob.Integration.Tests.ErrorExamples;

namespace Grob.Torture;

/// <summary>
/// The known-findings list (prompt §1.4; gate report §4.7), checked in at
/// <see cref="CorpusSlice.KnownFindingsPath"/>. It keeps the CI slice green while a register row
/// is open. A signature does not identify a root cause, so each entry names its row and the
/// programs it covers, and only those programs are suppressed: a new program with a known
/// signature is a finding until it is triaged. A row that closes takes its entries with it.
/// </summary>
/// <param name="entries">The entries, in file order.</param>
internal sealed class KnownFindings(IReadOnlyList<KnownEntry> entries) {
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    /// <summary>The entries, in file order.</summary>
    public IReadOnlyList<KnownEntry> Entries => entries;

    /// <summary>Reads the list from its JSON form, <c>{ "entries": [ { "row", "signature", "programs" } ] }</c>.</summary>
    /// <exception cref="JsonException">The text is not a well-formed list.</exception>
    public static KnownFindings Parse(string json) =>
        new(JsonSerializer.Deserialize<Document>(json, Options)!.Entries);

    /// <summary>The register row that covers <paramref name="program"/>'s finding, or <see langword="null"/>.</summary>
    public string? RowFor(string program, Signature signature) =>
        entries.FirstOrDefault(e => e.Signature == signature.Key && e.Programs.Contains(program, StringComparer.Ordinal))?.Row;

    /// <summary>
    /// What is wrong with the list against <paramref name="register"/>: an entry whose row is not
    /// in the register, or is closed, and an entry that names no programs.
    /// </summary>
    public IReadOnlyList<string> Problems(DeferredWorkRegister register) {
        var problems = new List<string>();
        foreach (KnownEntry entry in entries) {
            if (register.ClosedBy.TryGetValue(entry.Row, out string? closedBy)) {
                problems.Add($"known finding row {entry.Row} is closed by {closedBy}; remove its entries");
            } else if (!register.Open.Contains(entry.Row)) {
                problems.Add($"known finding row {entry.Row} is not in the register");
            }
            if (entry.Programs.Count == 0) {
                problems.Add($"known finding for {entry.Row} ({entry.Signature}) names no programs");
            }
        }
        return problems;
    }

    private sealed record Document(IReadOnlyList<KnownEntry> Entries);
}
