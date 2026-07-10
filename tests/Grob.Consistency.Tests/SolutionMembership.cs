using System.Xml.Linq;

namespace Grob.Consistency.Tests;

/// <summary>
/// The test-project membership gate (D-335). A pure comparison of the project paths a
/// <c>.slnx</c> file references against the test-project <c>.csproj</c> files actually
/// on disk, plus the disk-enumeration side that feeds it. Membership is self-relative —
/// it never compares against a frozen count — in the same shape as D-316's error-code
/// count agreement and D-308's <c>ErrorCatalog</c> agreement.
/// </summary>
internal static class SolutionMembership {
    /// <summary>
    /// Returns the subset of <paramref name="discoveredCsprojPaths"/> that
    /// <paramref name="slnxXml"/> does not reference. Comparison is case-insensitive
    /// and separator-normalised, since <c>.slnx</c> project paths are written with
    /// backslashes but a discovered path could carry either separator.
    /// </summary>
    /// <param name="slnxXml">The <c>.slnx</c> file content.</param>
    /// <param name="discoveredCsprojPaths">Repo-root-relative <c>.csproj</c> paths found on disk.</param>
    /// <returns>The orphaned paths, in the original casing/separator form they were supplied in.</returns>
    public static IReadOnlyList<string> FindOrphans(
        string slnxXml, IReadOnlyList<string> discoveredCsprojPaths) {
        var referenced = XDocument.Parse(slnxXml)
            .Descendants("Project")
            .Select(e => e.Attribute("Path")?.Value)
            .OfType<string>()
            .Select(Normalise)
            .ToHashSet(StringComparer.Ordinal);

        return discoveredCsprojPaths
            .Where(path => !referenced.Contains(Normalise(path)))
            .ToList();
    }

    /// <summary>
    /// Enumerates every <c>*.Tests.csproj</c> under the repository root, skipping
    /// build-output directories. The root is the whole repository, not just
    /// <c>tests/</c> — a real test project (<c>Grob.BenchCheck.Tests</c>) lives under
    /// <c>tooling/</c>, colocated with the tool it tests, so restricting to
    /// <c>tests/</c> would leave it unwatched.
    /// </summary>
    /// <param name="repoRoot">The repository root.</param>
    /// <returns>Repo-root-relative paths of every discovered test project.</returns>
    public static IReadOnlyList<string> DiscoverTestProjects(string repoRoot)
        => Directory.EnumerateFiles(repoRoot, "*.Tests.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .ToList();

    private static string Normalise(string path)
        => path.Replace('/', '\\').ToLowerInvariant();
}
