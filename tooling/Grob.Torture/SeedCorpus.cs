using System.Text;

using Grob.Integration.Tests.PublicSamples;

namespace Grob.Torture;

/// <summary>
/// The seed corpus (gate report §1.3), found by scanning the repository, never registered: the
/// test fixtures, the error examples, the benchmark fixtures, the public samples' <c>grob</c>
/// blocks and the raw string literals in the test projects. Build output under <c>bin</c> and
/// <c>obj</c> and the wiki's <c>ADR/</c> pages are left out, as the public samples gate leaves
/// them out. A source seen twice is kept once, under the first origin in load order.
/// </summary>
internal static class SeedCorpus {
    /// <summary>The file that marks the repository root.</summary>
    public const string SolutionFile = "Grob.slnx";

    private const ulong FnvOffset = 0xcbf29ce484222325UL;
    private const ulong FnvPrime = 0x100000001b3UL;

    /// <summary>Every seed under <paramref name="root"/>, in a fixed order: by kind, then by origin, then by position.</summary>
    public static IReadOnlyList<Seed> Load(string root) {
        IEnumerable<(SeedKind Kind, string Origin, string Source)> found =
            Files(root, "tests/fixtures", "*.grob").Select(f => (SeedKind.Fixture, f, Read(root, f)))
            .Concat(Files(root, "docs/errors/examples", "*.grob").Select(f => (SeedKind.ErrorExample, f, Read(root, f))))
            .Concat(Files(root, "bench/Grob.Benchmarks/Fixtures", "*.grob").Select(f => (SeedKind.Benchmark, f, Read(root, f))))
            .Concat(PublicSamplePages(root).SelectMany(p =>
                SampleExtractor.Extract(p, Read(root, p)).Select(b => (SeedKind.PublicSample, p, b.Source))))
            .Concat(Files(root, "tests", "*.cs").SelectMany(f =>
                RawLiteralExtractor.Extract(Read(root, f)).Select(s => (SeedKind.RawLiteral, f, s))));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        return [.. found
            .Where(s => seen.Add(s.Source))
            .Select(s => new Seed($"{s.Origin}@{Fnv1a64(s.Source):x16}", s.Kind, s.Origin, s.Source))];
    }

    /// <summary>The nearest directory at or above <paramref name="from"/> that holds <see cref="SolutionFile"/>.</summary>
    /// <exception cref="DirectoryNotFoundException">No directory above holds it.</exception>
    public static string LocateRepositoryRoot(string from) {
        for (DirectoryInfo? dir = new(from); dir is not null; dir = dir.Parent) {
            if (File.Exists(Path.Join(dir.FullName, SolutionFile))) return dir.FullName;
        }
        throw new DirectoryNotFoundException($"no {SolutionFile} at or above {from}");
    }

    /// <summary>
    /// FNV-1a over the UTF-8 bytes of <paramref name="text"/>. Not <see cref="string.GetHashCode()"/>,
    /// which is randomised per process, so an id would not survive a restart (gate report §4.5).
    /// </summary>
    public static ulong Fnv1a64(string text) {
        ulong hash = FnvOffset;
        foreach (byte b in Encoding.UTF8.GetBytes(text)) {
            hash = unchecked((hash ^ b) * FnvPrime);
        }
        return hash;
    }

    private static string Read(string root, string relative) =>
        File.ReadAllText(Path.Join(root, relative)).ReplaceLineEndings("\n");

    // Repository-relative paths with forward slashes, in ordinal order, outside bin and obj.
    private static IEnumerable<string> Files(string root, string directory, string pattern) {
        string start = Path.Join(root, directory);
        if (!Directory.Exists(start)) return [];
        return Directory.EnumerateFiles(start, pattern, SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => !f.Split('/').Any(segment => segment is "bin" or "obj"))
            .Order(StringComparer.Ordinal);
    }

    // The public samples gate's pages (D-430 Decision 1): README.md and the wiki without ADR/.
    private static IEnumerable<string> PublicSamplePages(string root) {
        IEnumerable<string> readme = File.Exists(Path.Join(root, "README.md")) ? ["README.md"] : [];
        return readme.Concat(Files(root, "docs/wiki", "*.md").Where(p => !p.StartsWith("docs/wiki/ADR/", StringComparison.Ordinal)));
    }
}
