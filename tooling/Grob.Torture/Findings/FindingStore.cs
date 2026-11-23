using System.Text.Json;

namespace Grob.Torture.Findings;

/// <summary>
/// The findings on disk: <c>&lt;root&gt;/&lt;signature-hash&gt;/&lt;program-hash&gt;.json</c> (gate report §4.7). The
/// strategy's §10 names a finding by its signature alone, which would let one program's record
/// replace another's, so a record is keyed by its program under the directory of its signature. A
/// record is created with <see cref="FileMode.CreateNew"/> and never overwritten: a second run over
/// the same program leaves the first record as it was.
/// </summary>
/// <param name="root">The directory the records go under, created when the first is written.</param>
internal sealed class FindingStore(string root) {
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    /// <summary>
    /// Writes <paramref name="record"/>. Returns <see langword="false"/> and changes nothing when a
    /// record for the program already exists under the signature.
    /// </summary>
    public bool Add(FindingRecord record) {
        string path = PathFor(record.Signature, record.ProgramId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try {
            using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(stream, record, Options);
            return true;
        } catch (IOException) when (File.Exists(path)) {
            return false;
        }
    }

    /// <summary>Every record under <paramref name="signature"/>, in file-name order.</summary>
    public IReadOnlyList<FindingRecord> Records(string signature) {
        string directory = Path.Join(root, Hash(signature));
        if (!Directory.Exists(directory)) return [];
        return [.. Directory.EnumerateFiles(directory, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(f => JsonSerializer.Deserialize<FindingRecord>(File.ReadAllText(f), Options)!)];
    }

    /// <summary>The path of the record for a program under a signature.</summary>
    public string PathFor(string signature, string programId) => Path.Join(root, Hash(signature), Hash(programId) + ".json");

    private static string Hash(string text) => SeedCorpus.Fnv1a64(text).ToString("x16");
}
