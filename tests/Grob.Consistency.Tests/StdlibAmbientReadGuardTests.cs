using Grob.DriftCheck;
using Xunit;

namespace Grob.Consistency.Tests;

/// <summary>
/// R-32's source guard: no file under <c>src/Grob.Stdlib/</c> reads the host's local time zone
/// or current directory. Those reads go through <c>IClock.LocalZone</c> and
/// <c>IFileSystem.CurrentDirectory</c>, so a host (the playground, the test harness) controls
/// them. A text scan, like the layout-name guard; a failure names the file and the pattern.
/// </summary>
public sealed class StdlibAmbientReadGuardTests {
    private static readonly string[] ForbiddenSubstrings = [
        "TimeZoneInfo.Local",
        "ToLocalTime(",
        "AssumeLocal",
        "Directory.GetCurrentDirectory",
        "Environment.CurrentDirectory",
    ];

    private const string GetFullPathCall = "Path.GetFullPath(";

    /// <summary>The one-argument form's name in a failure message.</summary>
    internal const string OneArgumentGetFullPath = "one-argument Path.GetFullPath(";

    /// <summary>Returns each forbidden pattern <paramref name="source"/> contains.</summary>
    internal static IReadOnlyList<string> ForbiddenReads(string source) {
        var found = ForbiddenSubstrings.Where(p => source.Contains(p, StringComparison.Ordinal)).ToList();
        if (HasOneArgumentGetFullPath(source)) {
            found.Add(OneArgumentGetFullPath);
        }
        return found;
    }

    /// <summary>
    /// True when some <c>Path.GetFullPath(</c> call has no comma at its own nesting level, so it
    /// resolves against the process's current directory. An unclosed call counts as one-argument.
    /// </summary>
    private static bool HasOneArgumentGetFullPath(string source) {
        for (int at = source.IndexOf(GetFullPathCall, StringComparison.Ordinal);
             at >= 0;
             at = source.IndexOf(GetFullPathCall, at + 1, StringComparison.Ordinal)) {
            if (!HasTopLevelComma(source, at + GetFullPathCall.Length)) {
                return true;
            }
        }
        return false;
    }

    private static bool HasTopLevelComma(string source, int start) {
        int depth = 0;
        int i = start;
        while (i < source.Length) {
            switch (source[i]) {
                case '"' or '\'':
                    i = LiteralEnd(source, i);
                    break;
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}' when depth == 0:
                    return false;
                case ')' or ']' or '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return true;
            }
            i++;
        }
        return false;
    }

    /// <summary>
    /// The index of the quote that closes the string or char literal opened at
    /// <paramref name="open"/>, so a comma inside it is not read as an argument separator. A
    /// verbatim string doubles its quotes; any other literal escapes with a backslash.
    /// </summary>
    private static int LiteralEnd(string source, int open) {
        char quote = source[open];
        bool verbatim = (open > 0 && source[open - 1] == '@')
            || (open > 1 && source[open - 1] == '$' && source[open - 2] == '@');
        int i = open + 1;
        while (i < source.Length) {
            if (!verbatim && source[i] == '\\') {
                i += 2;
            } else if (source[i] != quote) {
                i++;
            } else if (verbatim && i + 1 < source.Length && source[i + 1] == quote) {
                i += 2;
            } else {
                return i;
            }
        }
        return source.Length;
    }

    [Theory]
    [InlineData("var z = TimeZoneInfo.Local;", "TimeZoneInfo.Local")]
    [InlineData("d.ToLocalTime()", "ToLocalTime(")]
    [InlineData("DateTimeStyles.AssumeLocal", "AssumeLocal")]
    [InlineData("Directory.GetCurrentDirectory()", "Directory.GetCurrentDirectory")]
    [InlineData("string d = Environment.CurrentDirectory;", "Environment.CurrentDirectory")]
    [InlineData("Path.GetFullPath(p)", OneArgumentGetFullPath)]
    [InlineData("Path.GetFullPath(p.Length == 0 ? \".\" : p)", OneArgumentGetFullPath)]
    [InlineData("Path.GetFullPath(Join(a, b))", OneArgumentGetFullPath)]
    // A comma inside a literal is not an argument separator.
    [InlineData("Path.GetFullPath(\"reports,2026\")", OneArgumentGetFullPath)]
    [InlineData("Path.GetFullPath(@\"a\"\",b\")", OneArgumentGetFullPath)]
    [InlineData("Path.GetFullPath(p + ',')", OneArgumentGetFullPath)]
    [InlineData("Path.GetFullPath(\"a\\\",b\")", OneArgumentGetFullPath)]
    public void ForbiddenReads_PlantedRead_IsReported(string source, string pattern) {
        Assert.Equal([pattern], ForbiddenReads(source));
    }

    [Theory]
    [InlineData("Path.GetFullPath(p, fileSystem.CurrentDirectory)")]
    [InlineData("Path.GetFullPath(Join(a, b), dir)")]
    [InlineData("TimeZoneInfo.ConvertTime(d, _clock.LocalZone)")]
    [InlineData("TimeZoneInfo.FindSystemTimeZoneById(id)")]
    public void ForbiddenReads_CapabilityRead_IsNotReported(string source) {
        Assert.Empty(ForbiddenReads(source));
    }

    [Fact]
    public void StdlibSources_ReadNoAmbientZoneOrDirectory() {
        string stdlib = Path.Join(RepoPaths.RepoRoot(), "src", "Grob.Stdlib");
        string[] files = [.. Directory.EnumerateFiles(stdlib, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildOutput(stdlib, f))];

        var failures = files
            .SelectMany(f => ForbiddenReads(File.ReadAllText(f))
                .Select(p => $"{Path.GetRelativePath(stdlib, f)}: {p}"))
            .ToList();

        Assert.True(files.Length > 10, $"Scanned only {files.Length} files under {stdlib}; the walk may be broken.");
        Assert.True(failures.Count == 0, "Ambient reads in Grob.Stdlib (R-32): " + string.Join("; ", failures));
    }

    private static bool IsBuildOutput(string root, string file) {
        string first = Path.GetRelativePath(root, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return first is "bin" or "obj";
    }
}
