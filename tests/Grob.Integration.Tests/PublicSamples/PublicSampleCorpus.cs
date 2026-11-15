namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// The public samples on disk (D-430 Decision 1): <c>README.md</c> and every page under
/// <c>docs/wiki/</c> except <c>docs/wiki/ADR/</c>, read from the source tree at test time.
/// Pages are discovered by glob and blocks by scanning, never registered, so a new page is in the
/// gate the moment it exists. The fixture environment (D-430 Decision 8) lives here too.
/// </summary>
internal static class PublicSampleCorpus {
    private static readonly Lazy<string> Root = new(Locate);

    /// <summary>The repository root.</summary>
    public static string RepositoryRoot => Root.Value;

    /// <summary>
    /// One environment for the whole corpus: fake values for exactly the variables the runnable
    /// blocks read.
    /// </summary>
    public static IReadOnlyDictionary<string, string> FixtureEnvironment { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal) {
            ["ADO_PAT"] = "fixture-ado-pat",
            ["BASE_URL"] = "https://example.invalid",
        };

    /// <summary>The corpus pages, repository-relative with forward slashes, in ordinal order.</summary>
    public static IReadOnlyList<string> Pages() {
        string wiki = Path.Join(RepositoryRoot, "docs", "wiki");
        IEnumerable<string> wikiPages = Directory.EnumerateFiles(wiki, "*.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(RepositoryRoot, f).Replace('\\', '/'))
            .Where(p => !p.StartsWith("docs/wiki/ADR/", StringComparison.Ordinal));
        return [.. new[] { "README.md" }.Concat(wikiPages.Order(StringComparer.Ordinal))];
    }

    /// <summary>A page's text.</summary>
    public static string Read(string page) => File.ReadAllText(Path.Join(RepositoryRoot, page));

    /// <summary>Every block in the corpus, page by page.</summary>
    public static IReadOnlyList<SampleBlock> Blocks() =>
        [.. Pages().SelectMany(p => SampleExtractor.Extract(p, Read(p)))];

    /// <summary>
    /// The fixture variables no block names as a double-quoted string literal. A lexical check: it
    /// does not depend on which cases ran, and it fails the moment the corpus stops reading one.
    /// </summary>
    public static IReadOnlyList<string> UnreadFixtures(
            IReadOnlyDictionary<string, string> fixtures, IReadOnlyList<SampleBlock> blocks) =>
        [.. fixtures.Keys
            .Where(name => !blocks.Any(b => b.Source.Contains($"\"{name}\"", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)];

    private static string Locate() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            if (File.Exists(Path.Join(dir.FullName, "README.md"))
                    && Directory.Exists(Path.Join(dir.FullName, "docs", "wiki"))) {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the repository root (README.md beside docs/wiki) by walking up from {AppContext.BaseDirectory}.");
    }
}
