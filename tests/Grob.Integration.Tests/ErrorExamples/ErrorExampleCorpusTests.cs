using System.Text;

using Grob.Cli;
using Grob.Runtime;
using Grob.Stdlib;

using Xunit;

namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The behavioural layer over the live corpus (D-428): every pair runs through
/// <see cref="RunCommand"/>'s own pipeline via its capabilities seam, and the full ordered list of
/// diagnostics the CLI writes is compared with the list its master states. One case per pair,
/// named by its slug.
/// </summary>
public sealed class ErrorExampleCorpusTests {
    private static readonly DateTime FixedInstant = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Lazy<QuarantineManifest> Manifest = new(LoadManifest);

    public static TheoryData<string> Pairs => [.. ErrorExampleCorpus.Slugs()];

    /// <summary>
    /// Every pair matches its master, or is quarantined and fails exactly as its manifest row
    /// records. A quarantined pair is run like any other, never skipped: a skip would hide a pair
    /// that starts passing.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void Pair_MatchesItsMasterOrItsQuarantineRow(string slug) {
        IReadOnlyList<ExampleDiagnostic> expected = ExpectedDiagnostics(slug);
        CliDiagnostics actual = RunPair(slug);

        Assert.True(actual.Problems.Count == 0, $"{slug}:\n  {string.Join("\n  ", actual.Problems)}");
        string? verdict = PairVerdict.Check(slug, expected, actual.Diagnostics, Manifest.Value.Rows.GetValueOrDefault(slug));
        Assert.True(verdict is null, $"{slug}:\n{verdict}");
    }

    [Fact]
    public void QuarantineManifest_IsWellFormed() {
        Assert.True(Manifest.Value.Problems.Count == 0, string.Join("\n", Manifest.Value.Problems));
    }

    private static QuarantineManifest LoadManifest() {
        string path = Path.Join(ErrorExampleCorpus.Directory, QuarantineManifest.FileName);
        string text = File.Exists(path) ? File.ReadAllText(path) : "";
        NativeCatalogue catalogue = StdlibCatalogue.ForCompileOnly();
        return QuarantineManifest.Parse(text,
            ErrorExampleCorpus.Slugs().ToHashSet(StringComparer.Ordinal),
            DeferredWorkRegister.Parse(File.ReadAllText(ErrorExampleCorpus.OpenQuestionsPath)),
            catalogue.IsNamespace);
    }

    /// <summary>The master's diagnostics as tuples; the static layer guarantees each has a location.</summary>
    internal static IReadOnlyList<ExampleDiagnostic> ExpectedDiagnostics(string slug) =>
        ErrorExampleMaster.Parse(File.ReadAllText(ErrorExampleCorpus.MasterPath(slug))).Diagnostics
            .Where(d => d.Location is not null)
            .Select(d => new ExampleDiagnostic(d.Severity, d.Code, d.Location!.File, d.Location.Line, d.Location.Column))
            .ToList();

    /// <summary>
    /// Runs one pair through the CLI's pipeline over synthetic capabilities: an empty environment,
    /// a fixed clock and a constant random source, with closed stdin. The path passed is absolute,
    /// so no test changes the process's working directory.
    /// </summary>
    internal static CliDiagnostics RunPair(string slug) {
        string path = ErrorExampleCorpus.SourcePath(slug);
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        var command = new RunCommand(stdout, stderr, TextReader.Null, verbose: false,
            () => new HostCapabilities(new FixedRandomSource(), new MapEnvironment(), new FixedClock(FixedInstant)));

        command.Run(path);
        return CliDiagnosticReader.Read(stderr.ToString(), path);
    }
}
