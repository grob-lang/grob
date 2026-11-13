namespace Grob.Integration.Tests.ErrorExamples;

/// <summary>
/// The error-examples corpus on disk, <c>docs/errors/examples/</c>, located by walking up from
/// <see cref="AppContext.BaseDirectory"/> as <c>ValidationScriptCorpusTests</c> does. Pairs are
/// discovered as directories, never registered: a new pair is in the gate the moment it exists.
/// </summary>
internal static class ErrorExampleCorpus {
    private static readonly Lazy<string> Root = new(Locate);

    public static string Directory => Root.Value;

    public static string ReadmePath => Path.Join(Directory, "README.md");

    public static IReadOnlyList<string> Slugs() =>
        System.IO.Directory.EnumerateDirectories(Directory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();

    public static string SourcePath(string slug) => Path.Join(Directory, slug, $"{slug}.grob");

    public static string MasterPath(string slug) => Path.Join(Directory, slug, $"{slug}.expected.txt");

    private static string Locate() {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null) {
            string candidate = Path.Join(dir.FullName, "docs", "errors", "examples");
            if (System.IO.Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate docs/errors/examples by walking up from {AppContext.BaseDirectory}.");
    }
}
