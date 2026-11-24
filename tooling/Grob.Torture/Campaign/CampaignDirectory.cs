using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grob.Torture.Campaign;

/// <summary>
/// A campaign's files on disk, under one directory:
/// <list type="bullet">
///   <item><description><c>campaign.json</c>, its settings, and <c>plan.txt</c>, one planned program a line;</description></item>
///   <item><description><c>results/shard-&lt;k&gt;.jsonl</c>, one line per program a worker ran;</description></item>
///   <item><description><c>journal/shard-&lt;k&gt;.txt</c>, the program and phase a worker is in (<see cref="Journal"/>);</description></item>
///   <item><description><c>findings/</c>, the records (<see cref="Findings.FindingStore"/>), and <c>unminimised.txt</c>, the programs whose records were not minimised;</description></item>
///   <item><description><c>report.md</c>, the report.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// A worker that dies can leave the last line of its results cut short, so reading skips a line
/// that is not whole, and appending starts a new line first. Each result is appended and closed at
/// once, so a death loses at most the program it happened in, which the journal names.
/// </remarks>
/// <param name="root">The campaign's directory.</param>
internal sealed class CampaignDirectory(string root) {
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>The campaign's directory.</summary>
    public string Root => root;

    /// <summary>Where the finding records go.</summary>
    public string FindingsPath => Path.Join(root, "findings");

    /// <summary>Where the report goes.</summary>
    public string ReportPath => Path.Join(root, "report.md");

    private string SettingsPath => Path.Join(root, "campaign.json");

    private string PlanPath => Path.Join(root, "plan.txt");

    private string UnminimisedPath => Path.Join(root, "unminimised.txt");

    /// <summary>A shard's journal.</summary>
    public string JournalPath(int shard) => Path.Join(root, "journal", $"shard-{shard}.txt");

    /// <summary>A shard's results.</summary>
    public string ResultsPath(int shard) => Path.Join(root, "results", $"shard-{shard}.jsonl");

    /// <summary>The campaign's settings, or <see langword="null"/> when no campaign has started here.</summary>
    public CampaignSettings? ReadSettings() =>
        File.Exists(SettingsPath) ? JsonSerializer.Deserialize<CampaignSettings>(File.ReadAllText(SettingsPath), Options) : null;

    /// <summary>Records the campaign's settings.</summary>
    public void WriteSettings(CampaignSettings settings) {
        Directory.CreateDirectory(root);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Options));
    }

    /// <summary>The plan, in order.</summary>
    public IReadOnlyList<PlannedProgram> ReadPlan() =>
        [.. File.ReadLines(PlanPath).Select(line => line.Split('\t', 2)).Select(p => new PlannedProgram(p[1], p[0]))];

    /// <summary>Records the plan: one program a line, its kind then its id.</summary>
    public void WritePlan(IReadOnlyList<PlannedProgram> plan) {
        Directory.CreateDirectory(root);
        File.WriteAllLines(PlanPath, plan.Select(p => $"{p.Kind}\t{p.Id}"));
    }

    /// <summary>A shard's results in the order they were appended, skipping a line a death cut short.</summary>
    public IReadOnlyList<ProgramResult> ReadResults(int shard) {
        string path = ResultsPath(shard);
        if (!File.Exists(path)) return [];
        return [.. File.ReadLines(path).Select(TryParse).OfType<ProgramResult>()];
    }

    /// <summary>Appends one result to a shard's results, after mending a line a death cut short.</summary>
    public void AppendResult(int shard, ProgramResult result) =>
        AppendLine(ResultsPath(shard), JsonSerializer.Serialize(result, Options));

    /// <summary>The programs whose records were not minimised, as <c>&lt;program&gt;\t&lt;reason&gt;</c>, in order.</summary>
    public IReadOnlyList<string> ReadUnminimised() => File.Exists(UnminimisedPath) ? [.. File.ReadLines(UnminimisedPath)] : [];

    /// <summary>Records that a program's record was not minimised, and why.</summary>
    public void AppendUnminimised(string programId, string reason) => AppendLine(UnminimisedPath, $"{programId}\t{reason}");

    private static ProgramResult? TryParse(string line) {
        try {
            return JsonSerializer.Deserialize<ProgramResult>(line, Options);
        } catch (JsonException) {
            return null;
        }
    }

    private static void AppendLine(string path, string line) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        if (stream.Length > 0) {
            stream.Seek(-1, SeekOrigin.End);
            if (stream.ReadByte() != '\n') stream.WriteByte((byte)'\n');
        }
        stream.Write(Encoding.UTF8.GetBytes(line + "\n"));
    }
}
